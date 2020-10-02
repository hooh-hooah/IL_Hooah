#if UNITY_EDITOR
using System;
using System.Collections;
#endif
using UnityEngine;

[ExecuteInEditMode]
public class FalseChainCreator : MonoBehaviour
{
    // --------------------------------------------------

    private static readonly GeometryUtilityUser guu = new GeometryUtilityUser();

    private readonly QuadRope qr = new QuadRope();
    private readonly int tluPrecision = 16;

    [Header("Other settings")] [Tooltip("If the length of the chain is not large enough it will be stretched")]
    public bool allowStretching;

    public Transform end;

    [Header("Rope")] [Tooltip("If true, Link parameters will be ignored")]
    public bool isRope;

    private float lastStartWidth, lastEndWidth;
    private Vector2 lastTarget;
    private Vector3 lastTrgRot;
    public Vector3 linkForward = new Vector3(0, -1, 0);
    private int linkJoints; // Border links included
    private Vector2[] linkPositions; // update every frame looking at QuadRope and the Y function

    [Header("Link")] public Transform linkPrefab; // Create as many links as linkJoints

    public Vector3 linkRotationAxis = new Vector3(0, 1, 0);
    private Quaternion linkRotationAxisRotation;
    public float linkSize = 1;

    private Transform[] linksTransforms;

    [Header("Number of links")] public int numberOfLinks = 15;

    [Tooltip("Bigger values will be slower")]
    public int quality = 30; // Number of samples to capture the curve deformation. More = slower

    private float ropeLength;
    public Material ropeMaterial;

    [Tooltip("If you want to make a rope instead of a chain, set this to false")]
    public bool rotateEvenLinks = true;

    private float scale;
    public bool showGizmos;

    [Header("Start and end points")] public Transform start;

    public Color startColor = Color.white, endColor = Color.white;
    public float startWidth = 0.1f, endWidth = 0.1f;

    [Tooltip(
        "Resolution of a Look Up Texture (LUT) needed to positionate correctly the links of the chains. The texture is shader to all the chains, that means that if a chain has a LUT with bigger resolution, lower resolutions will be ignored")]
    public int tluResolution = 128;

    [Tooltip("Always update the position and rotation of the chain links. If true, visibility checks will be skipped, saving some computation")]
    public bool updateWhenOffscreen;

    private bool wasRope;


    private void Start()
    {
        linkRotationAxisRotation = Quaternion.AngleAxis(90, linkRotationAxis);
        ropeLength = 1;
    }

    private void LateUpdate()
    {
        linkJoints = numberOfLinks + 1;
        if (linkJoints < 2)
            return;
        if (linkSize <= 0)
            linkSize = 0.001f;
        if (null == start || null == end)
            return;
        if (null == linkPrefab && !isRope)
            return;

        scale = numberOfLinks * linkSize;

        var sPos = start.position;
        var ePos = end.position;
        if (ePos.x == sPos.x)
            ePos.x += 0.00001f;
        if (ePos.z == sPos.z)
            ePos.z += 0.00001f;

        var fromTo = ePos - sPos;
        var fromToScaled = fromTo;
        var lScale = transform.lossyScale;
        fromToScaled.x /= lScale.x;
        fromToScaled.y /= lScale.y;
        fromToScaled.z /= lScale.z;

        transform.position = sPos;
        var xz = new Vector2(fromToScaled.x, fromToScaled.z);
        var target = new Vector2(xz.magnitude, fromToScaled.y) / scale;
        if (target != lastTarget || isRope != wasRope || startWidth != lastStartWidth || endWidth != lastEndWidth)
        {
            if (!updateWhenOffscreen && !guu.IsVisibleByCamera(new Bounds(sPos + fromTo * 0.5f, lScale * scale)))
                return;

            lastTarget = target;
            lastStartWidth = startWidth;
            lastEndWidth = endWidth;
            qr.SetTarget(target, tluResolution, tluPrecision, allowStretching);
            //previewTextureLookUp = QuadRope.LUT_2_Texture();

            SetLocalLinkPositions();

            if (isRope)
            {
                if (!wasRope)
                {
                    wasRope = true;
                    gameObject.AddComponent<LineRenderer>();
                }

                SetRopePositions();
            }
            else
            {
                if (wasRope)
                {
                    wasRope = false;
                    DestroyImmediate(GetComponent<LineRenderer>());
                }

                SetLinkPositions();
            }
        }

        var trgRot = new Vector3(xz.x, 0, xz.y);
        if (trgRot != lastTrgRot)
        {
            lastTrgRot = trgRot;
            transform.rotation = Quaternion.FromToRotation(Vector3.right, trgRot);
        }
    }

    private void OnDrawGizmosSelected()
    {
        LateUpdate();
        if (!showGizmos)
            return;

        if (linkJoints < 2)
            return;
        if (null == start || null == end)
            return;
        if (null == linkPrefab && !isRope)
            return;

        var p = transform.position;

        // Draw box
        Gizmos.color = Color.white;
        qr.GizmoDrawRect(p);

        // Draw rope curve
        Gizmos.color = Color.red;
        var lastP = p;
        for (var i = 0; i < quality; i++)
        {
            var i01 = (float) i / (quality - 1);
            var newP = p + (Vector3) qr.At(i01);
            Gizmos.DrawLine(lastP, newP);
            lastP = newP;
        }

        // Draw rope link joints
        Gizmos.color = Color.green;
        for (var i = 0; i < linkPositions.Length; i++) Gizmos.DrawWireSphere(p + (Vector3) linkPositions[i], 0.02f);

        // Update in editor to previsualize the setting
        linkRotationAxisRotation = Quaternion.AngleAxis(90, linkRotationAxis);
    }

    private float GetRopeUnitLength()
    {
        return qr.GetCurveRectified();
    }

    private void SetRopePositions()
    {
        if (linksTransforms != null)
        {
            while (transform.childCount > 0)
                DestroyImmediate(transform.GetChild(0).gameObject);
            linksTransforms = null;
        }

        var lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.sharedMaterial = ropeMaterial;
        lr.startWidth = startWidth;
        lr.endWidth = endWidth;
        lr.startColor = startColor;
        lr.endColor = endColor;
        lr.positionCount = linkPositions.Length;
        for (var i = 0; i < linkPositions.Length; i++) lr.SetPosition(i, linkPositions[i] * scale);
    }

    private void SetLinkPositions()
    {
        if (linksTransforms == null || linksTransforms.Length != numberOfLinks)
        {
            while (transform.childCount > 0)
                DestroyImmediate(transform.GetChild(0).gameObject);
            linksTransforms = new Transform[numberOfLinks];
            for (var i = 0; i < linksTransforms.Length; i++)
            {
                linksTransforms[i] = Instantiate(linkPrefab);
                linksTransforms[i].parent = transform;
                linksTransforms[i].localScale = Vector3.one;
            }
        }

        var eRot = end.localRotation;

        Vector3 lPos3 = linkPositions[0];
        for (var i = 0; i < linksTransforms.Length; i++)
        {
            var link = linksTransforms[i];
            Vector3 nextlPos3 = linkPositions[i + 1];

            var r = Quaternion.FromToRotation(linkForward, lPos3 - nextlPos3);
            if (rotateEvenLinks && (i & 1) == 0) // Rotate 90º odd/even
                r = r * linkRotationAxisRotation;
            r *= Quaternion.AngleAxis((i + eRot.y) * 5, linkRotationAxis);

            link.localPosition = lPos3 * scale;
            link.localRotation = r;

            lPos3 = nextlPos3;
        }
    }

    private void SetLocalLinkPositions()
    {
        if (linkPositions == null || linkPositions.Length != linkJoints) linkPositions = new Vector2[linkJoints];

        var link = 0;
        var v = qr.At(0);
        linkPositions[link++] = v;

        ropeLength = GetRopeUnitLength();

        var xOffset = 1f / (quality - 1);
        var linkDistance = 1f / numberOfLinks * ropeLength; // ropeLength [0..1]
        float checkedDistance = 0;
        float x = 0;

        // Positionate last = target (approx)
        while (link < linkPositions.Length - 1)
        {
            var xNew = x + xOffset;
            var vNew = qr.At(xNew);
            var distanciaSegmento = Vector2.Distance(vNew, v);
            if (checkedDistance + distanciaSegmento < linkDistance)
            {
                x = xNew;
                v = vNew;
                checkedDistance += distanciaSegmento;
            }
            else
            {
                var lerp = (linkDistance - checkedDistance) / distanciaSegmento;

                x = x + (xNew - x) * lerp; // Mathf.Lerp(x, xNew, lerp)
                v.x = v.x + (vNew.x - v.x) * lerp; // Mathf.Lerp(v, vNew, lerp)
                v.y = v.y + (vNew.y - v.y) * lerp; // Mathf.Lerp(v, vNew, lerp)
                linkPositions[link++] = v;
                checkedDistance = 0;
            }
        }

        linkPositions[link] = qr.GetTarget();
    }


    /////////////////////////
    // CLASS
    /////////////////////////

    private class QuadRope
    {
        // stores the desired distanceYAxis of SetTarget. upper right half of the full target unit range
        private static int lut_side;
        private static float[] lut; // look up texture
        private float distanceYAxis;
        private Vector2 target;

        /// <summary>
        ///     LUT is stored as a array of floats. This function will return the array as a Texture2D to easily visualize it
        /// </summary>
        /// <returns>Representation of the LUT</returns>
        public static Texture2D LUT_2_Texture()
        {
            var t = new Texture2D(lut_side, lut_side, TextureFormat.Alpha8, false, true);
            t.wrapMode = TextureWrapMode.Clamp;
            for (var y = 0; y < lut_side; y++)
            for (var x = 0; x < lut_side; x++)
            {
                var f = lut[x + y * lut_side];
                t.SetPixel(x, y, new Color(f, f, f, f));
            }

            t.Apply();
            return t;
        }

        private void GenerateLUT(int side, int precision)
        {
            lut_side = side;
            lut = new float[side * side];
            var invSide = 1f / side;
            // duplicate the second row to the first row because when target.x == 0 the rectified curve gives NaN (division by 0)
            for (var y = 0; y < side; y++) lut[0 + y * side] = CalculateDistanceYAxis(new Vector2(invSide, y * invSide), precision);
            for (var x = 1; x < side; x++)
            {
                var x2 = x * invSide;
                for (var y = 0; y < side; y++) lut[x + y * side] = CalculateDistanceYAxis(new Vector2(x2, y * invSide), precision);
            }
        }

        private float FilteredLUT(float x, float y)
        {
            x *= lut_side;
            y *= lut_side;
            var x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, lut_side - 1);
            var x1 = Mathf.Clamp(Mathf.CeilToInt(x), 0, lut_side - 1);
            var y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, lut_side - 1) * lut_side;
            var y1 = Mathf.Clamp(Mathf.CeilToInt(y), 0, lut_side - 1) * lut_side;
            var xx = x % 1;
            return Mathf.LerpUnclamped(
                Mathf.LerpUnclamped(lut[x0 + y0], lut[x1 + y0], xx),
                Mathf.LerpUnclamped(lut[x0 + y1], lut[x1 + y1], xx),
                y % 1);
        }

        private float CalculateDistanceYAxis(Vector2 target, int precision)
        {
            var y = 0.5f;
            var step = y * 0.5f;
            for (var i = 0; i < precision; i++)
            {
                var l = GetCurveRectified(target, y);
                if (Mathf.Approximately(l, 1))
                    return y;
                if (l > 1)
                    y -= step;
                else
                    y += step;
                step *= 0.5f;
            }

            return y;
        }

        /*
		 * Project cuadratic funcion on a deformed quad. Start and end of the chain on target and base

		perimeter = 2

		    + target
			|\_
			|  \_0.5
		 0.5|    \_
			|      \_+ base
		    |   x^2  |
			 \_inside|
			   \_    |0.5
			 0.5 \_  |
				   \_|
		*/

        // 5 indices
        public void SetTarget(Vector2 targetOffset, int resolutionLookUp, int precision, bool allowStretching)
        {
            if (lut == null || lut_side < resolutionLookUp) GenerateLUT(resolutionLookUp, precision);

            target = targetOffset;
            var magnitude = target.magnitude;
            if (magnitude > 1f)
            {
                // The chain will be stretched
                if (!allowStretching)
                {
                    target /= magnitude; // normalize
                    magnitude = 1;
                }

                distanceYAxis = 0.00001f;
            }
            else
            {
                // Aproximation that makes the rope measure always near 1
                distanceYAxis = CalculateDistanceYAxis(target);
            }
        }

        public float CalculateDistanceYAxis(Vector2 target)
        {
            // lookup using targetOffset (x, y)
            return FilteredLUT(target.x, Mathf.Abs(target.y));
        }

        // http://math.stackexchange.com/questions/389174/how-do-you-find-the-distance-between-two-points-on-a-parabola
        /// <summary>
        ///     Get the length of the hanging rope. It should be approx 1
        /// </summary>
        public float GetCurveRectified()
        {
            return GetCurveRectified(target, distanceYAxis);
        }

        public float GetCurveRectified(Vector2 target, float distanceYAxis)
        {
            /*
			function:
			float g = target.x;
			float h = -distanceYAxis;
			float j = target.y;
			x = x * 2 - 1
			f(x) := ((x / g * 2 - 1) ^ 2) * -h + j * x / g + h
			∫(√(1 + f'(x)^2),x)

			js to do the derive-to-c# faster
			String.prototype.replaceAll = function(search, replacement) {
				var target = this;
				return target.split(search).join(replacement);
			}
			a.replaceAll("^","").replaceAll("LN", "Mathf.Log").replaceAll("·", "*").replaceAll("√", "Mathf.Sqrt").replaceAll("ABS", "Mathf.Abs");
			*/

            //Integrate(1) - Integrate(0);
            return Integrate(target.x, target, distanceYAxis) - Integrate(0, target, distanceYAxis);
        }

        // target.x == 0 is not valid
        private float Integrate(float x, Vector2 target, float distanceYAxis)
        {
            var g = target.x;
            var h = -distanceYAxis;
            var j = target.y;

            var g2 = g * g;
            var hx = h * x;

            var jAdd4h = 4 * h + j;
            var sqrt1 = Mathf.Sqrt(64 * h * hx * x - 16 * g * hx * jAdd4h + g2 * g2 + g2 * jAdd4h * jAdd4h);
            var hx8_gjAdd4h = hx * 8 - g * jAdd4h;
            var h16 = 16 * h;

            var log = sqrt1 + hx8_gjAdd4h < 0.00000001f ? 0 : Mathf.Log(sqrt1 + hx8_gjAdd4h) * g2;

            return (log + sqrt1 * hx8_gjAdd4h / g2) / h16;
        }

        public Vector2 At(float i01)
        {
            return At(i01, target, distanceYAxis);
        }

        public Vector2 At(float i01, Vector2 target, float distanceYAxis)
        {
            var g = target.x;
            var h = -distanceYAxis;
            var j = target.y;

            var x = i01 * g;

            var xg21 = x / g * 2 - 1;
            return new Vector2(x, xg21 * xg21 * -h + j * x / g + h);
        }

        public void GizmoDrawRect(Vector3 p)
        {
            Gizmos.DrawLine(p, p + new Vector3(0, -distanceYAxis, 0));
            Gizmos.DrawLine(p + new Vector3(0, -distanceYAxis, 0), p + new Vector3(target.x, target.y - distanceYAxis, 0));
            Gizmos.DrawLine(p + new Vector3(target.x, target.y - distanceYAxis, 0), p + (Vector3) target);
            Gizmos.DrawLine(p + (Vector3) target, p);
        }

        public Vector2 GetTarget()
        {
            return target;
        }
    }

    private class GeometryUtilityUser
    {
        private readonly PlaneSWR[] planes = new PlaneSWR[6] {new PlaneSWR(), new PlaneSWR(), new PlaneSWR(), new PlaneSWR(), new PlaneSWR(), new PlaneSWR()};
        private Matrix4x4[] camarasVP = new Matrix4x4[2];
        private Camera[] cameras = new Camera[1];
        private int lastFrameCount;
        private int numberOfCameras;

        public bool IsVisibleByCamera(Bounds bounds)
        {
            if (lastFrameCount != Time.frameCount)
            {
                lastFrameCount = Time.frameCount;
                numberOfCameras = Camera.allCamerasCount;
                if (numberOfCameras > cameras.Length) cameras = new Camera[numberOfCameras];
#if UNITY_EDITOR
				ArrayList scenes = UnityEditor.SceneView.sceneViews;
				numberOfCameras += scenes.Count;
#endif

                if (numberOfCameras > camarasVP.Length) camarasVP = new Matrix4x4[numberOfCameras];

                for (int i = 0, l = Camera.GetAllCameras(cameras); i < l; i++)
                {
                    var cam = cameras[i];
                    camarasVP[i] = cam.projectionMatrix * cam.worldToCameraMatrix;
                }
#if UNITY_EDITOR
				for (int i = 0; i < scenes.Count; i++) {
					Camera cam = ((UnityEditor.SceneView) scenes[i]).camera;
					camarasVP[numberOfCameras - i - 1] = cam.projectionMatrix * cam.worldToCameraMatrix;
				}
#endif
            }

            for (var i = 0; i < numberOfCameras; i++)
            {
                CalculateFrustumPlanes(planes, camarasVP[i]); // cachear proyeccion además de las cámaras
                if (CheckFrustumVisibility(planes, bounds)) return true;
            }

            return false;
        }

        // http://www.cnblogs.com/bodong/p/4800018.html
        // worldToProjectionMatrix = camera.projectionMatrix * camera.worldToCameraMatrix
        // Planes: 0 = Left, 1 = Right, 2 = Down, 3 = Up, 4 = Near, 5 = Far
        private static void CalculateFrustumPlanes(PlaneSWR[] OutPlanes, Matrix4x4 worldToProjectionMatrix)
        {
            var RootVector0 = worldToProjectionMatrix.m30;
            var RootVector1 = worldToProjectionMatrix.m31;
            var RootVector2 = worldToProjectionMatrix.m32;
            var RootVector3 = worldToProjectionMatrix.m33;

            var ComVector0 = worldToProjectionMatrix.m00;
            var ComVector1 = worldToProjectionMatrix.m01;
            var ComVector2 = worldToProjectionMatrix.m02;
            var ComVector3 = worldToProjectionMatrix.m03;

            OutPlanes[0].Set(ComVector0 + RootVector0, ComVector1 + RootVector1, ComVector2 + RootVector2, ComVector3 + RootVector3);
            OutPlanes[1].Set(-ComVector0 + RootVector0, -ComVector1 + RootVector1, -ComVector2 + RootVector2, -ComVector3 + RootVector3);

            ComVector0 = worldToProjectionMatrix.m10;
            ComVector1 = worldToProjectionMatrix.m11;
            ComVector2 = worldToProjectionMatrix.m12;
            ComVector3 = worldToProjectionMatrix.m13;

            OutPlanes[2].Set(ComVector0 + RootVector0, ComVector1 + RootVector1, ComVector2 + RootVector2, ComVector3 + RootVector3);
            OutPlanes[3].Set(-ComVector0 + RootVector0, -ComVector1 + RootVector1, -ComVector2 + RootVector2, -ComVector3 + RootVector3);

            ComVector0 = worldToProjectionMatrix.m20;
            ComVector1 = worldToProjectionMatrix.m21;
            ComVector2 = worldToProjectionMatrix.m22;
            ComVector3 = worldToProjectionMatrix.m23;

            OutPlanes[4].Set(ComVector0 + RootVector0, ComVector1 + RootVector1, ComVector2 + RootVector2, ComVector3 + RootVector3);
            OutPlanes[5].Set(-ComVector0 + RootVector0, -ComVector1 + RootVector1, -ComVector2 + RootVector2, -ComVector3 + RootVector3);
        }

        //http://stagpoint.com/forums/threads/improved-intersection-tests-for-unity.8/
        /// <summary>
        ///     Check if a bound is visible from a frustum
        /// </summary>
        private static bool CheckFrustumVisibility(PlaneSWR[] frustumPlanes, Bounds bounds)
        {
            var boundsCenter = bounds.center;
            var boundExtents = bounds.extents;
            for (var i = 0; i < frustumPlanes.Length; i++)
            {
                var normal = frustumPlanes[i].normal;
                var normalAbs = frustumPlanes[i].normalAbs;

                // Compute the projection interval radius of box b onto L(t) = b.c + t * p.n
                //float r = Vector3.Dot(plane.normalAbs, boundExtents);
                var r = normalAbs.x * boundExtents.x + normalAbs.y * boundExtents.y + normalAbs.z * boundExtents.z;

                // Compute distance of box center from plane
                //float distance = Vector3.Dot(plane.normal, boundsCenter) + plane.distance;
                var distance = normal.x * boundsCenter.x + normal.y * boundsCenter.y + normal.z * boundsCenter.z + frustumPlanes[i].distance;


                if (distance < -r) // If the AABB lies behind *any* of the planes, there
                    // is no point in continuing with the rest of the test
                    return false;
            }

            return true;
        }

        private class PlaneSWR
        {
            public float distance;
            public Vector3 normal;
            public Vector3 normalAbs;

            public void Set(float InA, float InB, float InC, float InDistance)
            {
                normal.x = InA;
                normal.y = InB;
                normal.z = InC;

                var InverseMagnitude = 1.0f / normal.magnitude;
                normal.x *= InverseMagnitude;
                normal.y *= InverseMagnitude;
                normal.z *= InverseMagnitude;

                distance = InDistance * InverseMagnitude;
                normalAbs.x = normal.x > 0 ? normal.x : -normal.x;
                normalAbs.y = normal.y > 0 ? normal.y : -normal.y;
                normalAbs.z = normal.z > 0 ? normal.z : -normal.z;
            }
        }
    }
}