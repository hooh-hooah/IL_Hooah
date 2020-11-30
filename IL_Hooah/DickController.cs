﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using Random = System.Random;

public class DickController : MonoBehaviour
{
    public enum DickShapes
    {
        Pull = 0,
        Push = 1,
        PushForwardSkin = 2
    }

    // I don't want to search every single blendshape every fucking time.
    private static readonly Dictionary<string, DickShapes> _blendshapeDictionary = new Dictionary<string, DickShapes>
    {
        {"pull", DickShapes.Pull},
        {"push", DickShapes.Push},
        {"pushfskin", DickShapes.PushForwardSkin}
    };

    public AudioClip[] pewSounds;
    public AudioSource audioPlayer;
    private readonly Random _random = new Random();
    private Animator _animator;

    public void PlayPew()
    {
        if (!pewSounds.Any() || audioPlayer == null) return;
        audioPlayer.Stop();
        var randomIndex = _random.Next(0, pewSounds.Length - 1);
        audioPlayer.pitch =  Mathf.Min(2f, _animator != null ? _animator.speed : 1f) + Convert.ToSingle(_random.NextDouble()*0.2);
        audioPlayer.PlayOneShot(pewSounds[randomIndex]);
        // EventConsumer.EmitEvent(EventConsumer.EventType.Nomi);
    }
    
    public GameObject curveEnd;
    public GameObject curveMiddle;
    public GameObject curveStart;
    public GameObject[] dickChains;
    public SkinnedMeshRenderer dickMesh;

    public float dockDistance = 2.1f;

    [FormerlySerializedAs("steepness")] public float pullLength = 1f;

    public Transform pullProxy;
    public Transform pullProxyRoot;

    [FormerlySerializedAs("followClosestNavigator")]
    public bool useNearestNavigator;

    public bool useNearestProxy;

    private readonly List<int> _shapeKeyIndex = new List<int>();
    private bool _canMorph;
    private Transform[] _dickTransforms;
    private float _firstDistance;
    private DickNavigator dickNavigator;
    private float pullFactor;
    private Transform pullTransform;

    private Vector3 EndPos => curveEnd?.transform.position ?? Vector3.zero;
    private Vector3 MidPos => curveMiddle?.transform.position ?? Vector3.zero;
    private Vector3 StartPos => curveStart?.transform.position ?? Vector3.zero;
    private float BenisScale => transform.localScale.z;
    private float FirstDistance => _firstDistance * BenisScale;
    private int ChainsLength => (dickChains?.Length ?? 0) - 1;

    private float BenisLength =>
        Vector3.Distance(StartPos, MidPos) + Vector3.Distance(MidPos, EndPos);

    private void Start()
    {
        _animator = GetComponent<Animator>();
        _dickTransforms = dickChains.Select(o => o.transform).ToArray();
        foreach (var i in Enumerable.Range(0, dickMesh.sharedMesh.blendShapeCount))
        {
            var name = dickMesh.sharedMesh.GetBlendShapeName(i);
            if (_blendshapeDictionary.ContainsKey(name))
                _shapeKeyIndex.Insert((int) _blendshapeDictionary[name], i);
        }

        _canMorph = _shapeKeyIndex.Count == _blendshapeDictionary.Count;
        _firstDistance = BenisLength;

        StartCoroutine(nameof(UpdateProxyInformation));
    }

    private void Update()
    {
        // But we need to check if it's null every single frame.
        // and resharper said GameObject == null is expensive, and referenceequals is not
        // https://forum.unity.com/threads/optimizing-null-check-null-vs-bool-out-functions.482118/

        if (!ReferenceEquals(dickNavigator, null))
        {
            var dist = Vector3.Distance(dickNavigator.dickMidPoint.position, StartPos);
            var distFactor = dist / (dockDistance * 1.25f * BenisScale);
            var lerpFactor = 1f - Mathf.Clamp(distFactor - 1f, 0f, 1f);
            var up = curveStart.transform.up;
            curveMiddle.transform.position = Vector3.Lerp(StartPos + up * (1f * BenisScale), dickNavigator.dickMidPoint.position, lerpFactor);
            curveEnd.transform.position = Vector3.Lerp(StartPos + up * (2f * BenisScale), dickNavigator.dickEndPoint.position, lerpFactor);
        }

        var pTransform = pullTransform ? pullTransform : pullProxy;
        if (ReferenceEquals(pTransform, null)) return;

        var position = pullProxyRoot.position;
        var position1 = pTransform.position;
        var distance = Vector3.Distance(position, position1);
        var crossDot = Vector3.Dot((position - position1).normalized, pullProxyRoot.up); // 1 to -1, 0 is exact match.
        pullFactor = Mathf.Clamp(distance * crossDot / pullLength * BenisScale, -1f, 1f);
    }

    private void LateUpdate()
    {
        var distanceFraction = FirstDistance / BenisLength;
        var chainLength = distanceFraction / ChainsLength;
        var benisMiddlePoint = Vector3.Distance(StartPos, MidPos) / BenisLength;

        if (_canMorph && _shapeKeyIndex != null && _shapeKeyIndex.Count > 0)
        {
            if (pullFactor >= 0)
            {
                dickMesh.SetBlendShapeWeight(_shapeKeyIndex[(int) DickShapes.Push], 0f);
                dickMesh.SetBlendShapeWeight(_shapeKeyIndex[(int) DickShapes.PushForwardSkin], 0f);
                dickMesh.SetBlendShapeWeight(_shapeKeyIndex[(int) DickShapes.Pull], Mathf.Min(50, pullFactor * 100));
            }
            else
            {
                pullFactor *= -1;
                dickMesh.SetBlendShapeWeight(_shapeKeyIndex[(int) DickShapes.Push], pullFactor * 100);
                dickMesh.SetBlendShapeWeight(_shapeKeyIndex[(int) DickShapes.PushForwardSkin], pullFactor * 100);
                dickMesh.SetBlendShapeWeight(_shapeKeyIndex[(int) DickShapes.Pull], 0f);
            }
        }

        Transform prvTransform = null;
        for (var index = 0; index < _dickTransforms.Length; index++)
        {
            var notFirst = index != 0;
            var chainTransform = _dickTransforms[index];

            chainTransform.position = Linear(StartPos, MidPos, EndPos, benisMiddlePoint,
                notFirst ? index * chainLength * BenisScale : 0f);

            if (notFirst)
            {
                var dir = (chainTransform.position - prvTransform.position).normalized;
                if (dir != Vector3.zero)
                {
                    var q = Quaternion.LookRotation(dir, transform.right);
                    // how do i 
                    q *= Quaternion.Euler(90, 0, 0);
                    q *= Quaternion.Euler(0, 90, 0);
                    prvTransform.rotation = q;
                }
            }

            prvTransform = chainTransform;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (dickChains != null)
            foreach (var dickChain in dickChains)
            {
                var t = dickChain.transform;
                var p = t.position;
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(p, .1f);
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(p, p + t.forward * 1);
                Gizmos.color = Color.green;
                Gizmos.DrawLine(p, p + t.up * 1);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(p, p + t.right * 1);
            }

        if (pullProxyRoot != null)
        {
            var t = pullProxyRoot;
            var p = t.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(p, .1f);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(p, p + t.forward * 1);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(p, p + t.up * 1);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(p, p + t.right * 1);
        }
    }
#endif

    private IEnumerator UpdateProxyInformation()
    {
        while (true)
        {
            if (useNearestNavigator)
                if (DickNavigator.Instances.Count > 0)
                    dickNavigator = DickNavigator.Instances
                        .OrderBy(x => Vector3.Distance(x.dickMidPoint.position, StartPos))
                        .First();

            if (useNearestProxy)
            {
                if (DickPuller.Instances.Count > 0)
                    pullTransform = DickPuller.Instances
                        .OrderBy(x => Vector3.Distance(x.transform.position, pullProxyRoot.transform.position))
                        .First().gameObject.transform;
                else
                    pullTransform = pullProxy;
            }

            // We don't need to find Nearest Proxies every single frame.
            yield return new WaitForSeconds(.5f);
        }
    }

    private static Vector3 Linear(Vector3 p0, Vector3 p1, Vector3 p2, float mp, float t)
    {
        return t <= mp ? Vector3.Lerp(p0, p1, t * (1 / mp)) : Vector3.Lerp(p1, p2, (t - mp * 1f) / (1f - mp));
    }

    private static float QuadFloat(float d, float t = 1f, float b = 0f, float c = 1f)
    {
        return c * Mathf.Pow(t / d, 2) + b;
    }
}