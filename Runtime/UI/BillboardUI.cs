using UnityEngine;

namespace HP.Framework.UI
{
    /// <summary>
    /// Orient World Space Canvases and 3D UI elements to match the Main Camera rotation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BillboardUI : MonoBehaviour
    {
        private Camera cachedCamera;

        private void LateUpdate()
        {
            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
                if (cachedCamera == null)
                {
                    return;
                }
            }

            transform.rotation = cachedCamera.transform.rotation;
        }
    }
}


