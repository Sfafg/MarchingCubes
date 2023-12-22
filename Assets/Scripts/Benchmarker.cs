using System;
using System.IO;
using UnityEngine;
public struct Mark
{
    public Action func;
    public int sampleCount;
    public Mark(Action func, int sampleCount)
    {
        this.func = func;
        this.sampleCount = sampleCount;
    }
}
public class Benchmarker
{
    static public void Test(string name, Mark[] marks)
    {
        string filePath = Path.Combine("C:\\Projekty\\Unity\\MarchingCubes_v5.0\\Assets\\Logs", name);
        StreamWriter writer = new(filePath);

        foreach (Mark mark in marks)
        {
            mark.func.Invoke();
            mark.func.Invoke();
            mark.func.Invoke();

            for (int i = 0; i < mark.sampleCount; i++)
            {
                float start = Time.realtimeSinceStartup;
                mark.func.Invoke();
                float end = Time.realtimeSinceStartup;
                writer.Write((end * 1000 - start * 1000).ToString() + "\n");
            }
        }
        writer.Close();
    }
}
