using UnityEngine;

namespace PixelChroma
{
    public class SpinBackground : MonoBehaviour
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
            transform.Rotate(new Vector3(0, 0,1) * 10 * Time.deltaTime);
        }
    }
}
