using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DomainRandomization : MonoBehaviour
{
    // Whether to use randomization or not
    public static bool useRandomization;

    // Things that can be randomized (walls, lights, track)
    public Light[] lights;
    public GameObject[] walls;
    public GameObject track;

    // Randomization time interval
    public float randomizationSeconds;
    private float latestRandomizationTime;

    // Attributes that can be modified for lights
    private Color[] colors = { Color.red, Color.blue, Color.yellow, Color.green, Color.white};
    //private GameObject[] lightRanges = { };

    // Attributes that can be modified for walls
    public GameObject[] wallTextures;

    // Attributes that can be modified for track
    private Texture2D[] trackTextures = { };

    T pickRandom <T>(T[] array)
    {
        int index = (int)(Random.Range(0, array.Length));
        return array[index];
    }


    void randomizeLights()
    {
        foreach (Light l in lights)
        {
            l.color = pickRandom(colors);
            l.transform.position = new Vector3(
                Random.Range(-32.4f, 4.4f),
                Random.Range(10.9f, 19.9f),
                Random.Range(-6, 6)
            );
        }
    }

    void randomizeWalls()
    {
        walls = GameObject.FindGameObjectsWithTag("Wall");

        foreach(GameObject wall in walls)
        {
            Renderer renderer = wall.GetComponent<Renderer>();

            Color randomColor = new Color(Random.value, Random.value, Random.value);
            renderer.material.color = randomColor;

            //renderer.material.mainTexture = randomTexture;
        }
    }

    void randomizeTrack()
    {
        // Findet das Track-Objekt zur Laufzeit (falls das Tag "road_mesh" verwendet wird)
        GameObject track = GameObject.FindWithTag("road_mesh");

        if (track != null)
        {
            Renderer renderer = track.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Material klonen, damit die Farbe unabhängig geändert werden kann
                renderer.material = new Material(renderer.material);

                // Zufällige Farbe erzeugen
                Color randomColor = new Color(Random.value, Random.value, Random.value);
                renderer.material.color = randomColor;  // Setzt die Hauptfarbe des Materials
            }
        }
    }


    void randomizeDomain()
    {
        // randomizeLights();
        randomizeWalls();
        randomizeTrack();
    }



    // Start is called before the first frame update
    void Start()
    {
        useRandomization = true;
        latestRandomizationTime = Time.time;
    }

    // Update is called once per frame
    void Update()
    {
        if (useRandomization)
        {
            if (Time.time - latestRandomizationTime >= randomizationSeconds)
            {
                randomizeDomain();
                latestRandomizationTime = Time.time;
            }
        }
    }
}
