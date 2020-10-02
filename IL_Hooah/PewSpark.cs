using UnityEngine;

public class PewSpark : MonoBehaviour
{
    public ParticleSystem[] emitters;

    // Use this for initialization
    private void Start()
    {
    }

    // Update is called once per frame
    private void Update()
    {
    }

    public void FireEmitter()
    {
        foreach (var emitter in emitters)
        {
            emitter.randomSeed = (uint) Time.time + (uint) Random.Range(0, 100);
            emitter.Emit(1);
        }
    }
}