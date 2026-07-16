using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.SOResetSystem.Editor
{

// This class represents our Data layer.
// It is responsible ONLY for holding the configuration data.
public class SOResetSettings : ScriptableObject
{
    public List<ScriptableObject> trackedObjects = new List<ScriptableObject>();
}

}