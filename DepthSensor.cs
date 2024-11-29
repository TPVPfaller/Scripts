using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System; // For Convert.ToBase64String

public class DepthSensor : MonoBehaviour
{
    public Material material;
    public int height = 1080; // Change to desired height
    public int width = 1080;  // Change to desired width
    private Camera cam;
    private RenderTexture rt;
    private Texture2D tex;

    // Start is called before the first frame update
    void Start()
    {
        cam = GetComponent<Camera>();
        cam.depthTextureMode = DepthTextureMode.Depth;

        // Create a RenderTexture with the desired width and height
        rt = new RenderTexture(width, height, 24);
        cam.targetTexture = rt;

        // Create a Texture2D to read the RenderTexture later
        tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
    }

    void OnRenderImage(RenderTexture source, RenderTexture dest)
    {
        // Blit the source render texture to the destination render texture using the material
        Graphics.Blit(source, dest, material);

        // Read the render texture into the Texture2D (only if needed)
        ReadRenderTextureToTexture2D();
    }

    public string GetAlive()
    {
        return "Alive!";
    }

    void ReadRenderTextureToTexture2D()
    {
        // Set the active RenderTexture to the camera's RenderTexture
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = rt;

        // Read the pixels from the RenderTexture into the Texture2D
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        // Restore the original active RenderTexture
        RenderTexture.active = currentRT;
    }

    // Public function to get the image bytes in a specified format
    public byte[] GetImageBytes(string img_enc = "PNG")
    {
        // Read the render texture into the Texture2D
        ReadRenderTextureToTexture2D();

        // Return the image bytes in the requested format
        if (img_enc == "PNG")
        {
            return tex.EncodeToPNG();
        }
        else if (img_enc == "TGA")
        {
            return tex.EncodeToTGA();
        }
        else
        {
            return tex.EncodeToJPG();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}