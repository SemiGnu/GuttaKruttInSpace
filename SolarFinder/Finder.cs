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
        public class Finder
        {
            public string Axis { get; set; }
            public float Angle { get
                {
                    var max = _angles.Max();
                    var min = _angles.Min();
                    if (max - min > 180) max -= 360;
                    var angle = (max + min) / 2;
                    if (angle < 0) angle += 360;
                    return angle;
                } 
            }


            IMyMotorStator _rotor;
            IMySolarPanel _panel;

            float _lastValue;
            float[] _angles;
            int _i = 0;

            public Finder(IMyGridTerminalSystem grid, string axis)
            {
                Axis = axis;
                _rotor = grid.GetBlockWithName($"Rotor {Axis}") as IMyMotorStator;
                _panel = grid.GetBlockWithName($"Panel {Axis}") as IMySolarPanel;

                _lastValue = 0;
                _angles = new[] { _rotor.Angle, _rotor.Angle };
                _rotor.TargetVelocityRPM = 0.25f;

            }

            public void Main()
            {
                var newValue = _panel.MaxOutput;
                if (newValue < _lastValue)
                {
                    _rotor.TargetVelocityRPM *= -1;
                    _angles[_i] = _rotor.Angle;
                    _i = 1 - _i;
                }
                _lastValue = newValue;
            }
        }
    }
}
