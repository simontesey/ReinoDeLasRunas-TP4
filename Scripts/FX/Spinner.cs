using UnityEngine;

namespace ReinoRunas
{
    public class Spinner : MonoBehaviour
    {
        public Vector3 speed = new Vector3(0f, 90f, 0f);

        void Update() => transform.Rotate(speed * Time.deltaTime, Space.Self);
    }
}
