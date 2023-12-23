using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

public class Benchmarker
{
    class Mark
    {
        public class SubFunction
        {
            public float totalTime;
            public int samples;
            public SubFunction(float sample) { totalTime = sample; samples = 1; }
            public float AverageTime() { return totalTime / samples; }
        };
        public int argument;
        public int sampleCount;
        public float averageTime;
        public Dictionary<string, SubFunction> subFunctions;
        public Mark(int argument, int sampleCount)
        {
            this.argument = argument;
            this.sampleCount = sampleCount;
            averageTime = 0f;
            subFunctions = new Dictionary<string, SubFunction>();
        }

        private string TimeToString(float t)
        {
            string postfix = "s";
            if (t < 0.01f)
            {
                t *= 1000;
                postfix = "ms";
            }

            return t.ToString("0.###") + postfix;
        }
        public string ToString(string functionName)
        {
            string str = "[resolution: " + argument + ", sampleCount: " + sampleCount + "]\n";
            str += functionName + ": " + TimeToString(averageTime) + "\n";

            int longestFunctionName = 4;
            int longestTimeStr = 0;
            foreach (string subFunction in subFunctions.Keys)
            {
                longestFunctionName = Mathf.Max(longestFunctionName, subFunction.Length);
                longestTimeStr = Mathf.Max(longestTimeStr, TimeToString(subFunctions[subFunction].AverageTime()).Length);
            }
            float subTotal = 0;
            string timeString;
            foreach (string subFunction in subFunctions.Keys)
            {
                int spacing = longestFunctionName - subFunction.Length;
                timeString = TimeToString(subFunctions[subFunction].AverageTime());
                int timeSpacing = longestTimeStr - timeString.Length;
                float timePercentage = (subFunctions[subFunction].AverageTime() / averageTime) * 100;
                str += "\t" + subFunction + ": " + new string(' ', spacing) + timeString + new string(' ', timeSpacing + 4) + timePercentage.ToString("0.##") + "%\n";
                subTotal += subFunctions[subFunction].AverageTime();
            }
            timeString = TimeToString(averageTime - subTotal);
            float percentage = ((averageTime - subTotal) / averageTime) * 100;
            str += "\tSelf: " + new string(' ', longestFunctionName - 4) + timeString + new string(' ', longestTimeStr - timeString.Length + 4) + percentage.ToString("0.##") + "%\n";
            return str;
        }
    };

    string name;
    readonly Func<int, Mesh> func;
    List<Mark> marks;
    Mark currentMark;
    public Benchmarker(string name, Func<int, Mesh> func)
    {
        this.func = func;
        this.name = name;
        marks = new List<Mark>();
    }
    public void Run(int argument, int sampleCount)
    {
        func.Invoke(argument);
        func.Invoke(argument);

        currentMark = new(argument, sampleCount);
        for (int i = 0; i < sampleCount; i++)
        {
            float start = Time.realtimeSinceStartup;
            func.Invoke(argument);
            currentMark.averageTime += Time.realtimeSinceStartup - start;
        }
        currentMark.averageTime /= sampleCount;

        marks.Add(currentMark);
    }

    public void SubFunction(float sample, string functionName)
    {
        if (currentMark == null) return;
        if (currentMark.subFunctions.ContainsKey(functionName))
        {
            currentMark.subFunctions[functionName].totalTime += sample;
            currentMark.subFunctions[functionName].samples++;
        }
        else
        {
            currentMark.subFunctions.Add(functionName, new Mark.SubFunction(sample));
        }
    }

    public void EndTest()
    {
        MethodInfo methodInfo = func.Method;

        StreamWriter writer = new(Path.Combine("C:\\Projekty\\Unity\\MarchingCubes_v5.0\\Assets\\Logs", name + ".txt"));
        for (int i = 0; i < marks.Count - 1; i++)
        {
            writer.Write(marks[i].ToString(methodInfo.Name) + "\n");
        }
        writer.Write(marks[marks.Count - 1].ToString(methodInfo.Name));
        writer.Close();
    }
}
