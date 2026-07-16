using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using System;

[Serializable]
public class VibraForge : MonoBehaviour
{
    private TcpSender sender;
    private Dictionary<string, int> command;

    // Awake (not Start) so SendCommand is safe from any other Start call.
    void Awake()
    {
        sender = this.GetComponent<TcpSender>();
        command = new Dictionary<string, int>()
        {
            { "addr", -1 },
            { "mode", 0 },
            { "duty", 0 },
            { "freq", 2 }
        };
    }

    private void Update()
    {

    }

    public string DictionaryToString(Dictionary<string, int> dictionary)
    {
        string dictionaryString = "{";
        foreach (KeyValuePair<string, int> keyValues in dictionary)
        {
            dictionaryString += "\"" + keyValues.Key + "\": " + keyValues.Value + ", ";
        }
        return dictionaryString.TrimEnd(',', ' ') + "}";
    }

    public void SendCommand(int addr, int mode, int duty, int freq)
    {
        command["addr"] = addr;
        command["mode"] = mode;
        command["duty"] = duty;
        command["freq"] = freq;
        sender.SendData(DictionaryToString(command) + "\n");
    }
}
