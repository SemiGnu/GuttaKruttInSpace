using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class CustomDataHelper
        {
            public static string Get(string customData, string section, string name)
            {
                var value = customData.Substring(customData.IndexOf($"[{section}]"));
                value = value.Substring(value.IndexOf($"{name}"));
                var start = value.IndexOf("=") + 1;
                var end = value.IndexOf("\n");
                value = value.Substring(start, end - start);

                return value;
            }

            public static int GetInt (string customData, string section, string name)
            {
                var s = Get(customData, section, name);
                int value;
                int.TryParse(s, out value);
                return value;
            }
        }
    }
}
