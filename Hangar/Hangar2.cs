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
        public class Hangar2
        {
            List<IMyAirtightHangarDoor> _hangarDoors;
            List<IMyAirVent> _hangarAirVents;
            List<IMyDoor> _hangarSideDoors;
            List<IMyTextSurface> _hangarLcds;
            List<IMyLightingBlock> _hangarLights;

            public string Name { get; set; }
            public enum HangarStatus
            {
                Opening, Open, Closing, Closed
            }
            DoorStatus _doorStatus => _hangarDoors.First().Status;
            VentStatus _ventStatus => _hangarAirVents.First().Status;
            HangarStatus _hangarStatus;
            bool ShouldDepressurize => _hangarStatus == HangarStatus.Opening && _ventStatus == VentStatus.Pressurized;
            bool ShouldPressurize => _hangarStatus == HangarStatus.Closing && _doorStatus == DoorStatus.Closed;
            bool ShouldOpenDoors => _hangarStatus == HangarStatus.Opening && _ventStatus == VentStatus.Depressurized;
            bool ShouldCloseDoors => _hangarStatus == HangarStatus.Closing && _doorStatus == DoorStatus.Open;
            bool HasOpened => _hangarStatus == HangarStatus.Opening && _doorStatus == DoorStatus.Open;
            bool HasClosed => _hangarStatus == HangarStatus.Closing && _ventStatus == VentStatus.Pressurized;
            bool IsIdle => _hangarStatus == HangarStatus.Closed || _hangarStatus == HangarStatus.Open;

            public Hangar2(string name, IMyGridTerminalSystem grid)
            {
                Name = name;
                grid.GetBlockGroupWithName($"{name} Hangar Doors").GetBlocksOfType(_hangarDoors);
                grid.GetBlockGroupWithName($"{name} Hangar Air Vents").GetBlocksOfType(_hangarAirVents);
                grid.GetBlockGroupWithName($"{name} Hangar Side Doors").GetBlocksOfType(_hangarSideDoors);
                grid.GetBlockGroupWithName($"{name} Hangar LCDs").GetBlocksOfType(_hangarLcds);
                grid.GetBlockGroupWithName($"{name} Hangar Warning Lights").GetBlocksOfType(_hangarLights);
            }

            public void Main()
            {
                if (!IsIdle)
                {
                    UpdateState();
                }
                _hangarLights.ForEach(l => l.Enabled = !IsIdle);
                _hangarLcds.ForEach(l => l.WriteText($"{(VentStatus)(_ventStatus >> 2)}"));
            }

            public void Toggle()
            {
                if (_hangarStatus == HangarStatus.Open || _hangarStatus == HangarStatus.Opening)
                {
                    if (_doorStatus == DoorStatus.Open || _doorStatus == DoorStatus.Opening)
                    {
                        _hangarDoors.ForEach(d => d.CloseDoor());
                    }
                    if (_doorStatus == DoorStatus.Closed && (_ventStatus == VentStatus.Depressurized || _ventStatus == VentStatus.Depressurizing))
                    {
                        Pressurize();
                    }
                    _hangarStatus = HangarStatus.Closing;
                } 
                else
                {
                    if (_ventStatus == VentStatus.Depressurized && (_doorStatus == DoorStatus.Closed || _doorStatus == DoorStatus.Closing))
                    {
                        _hangarDoors.ForEach(d => d.OpenDoor());
                    }
                    if ((_ventStatus == VentStatus.Pressurized || _ventStatus == VentStatus.Pressurizing))
                    {
                        Depressurize();
                    }
                    _hangarStatus = HangarStatus.Opening;
                }
            }

            void UpdateState()
            {
                if (ShouldDepressurize) Depressurize();
                if (ShouldPressurize) Pressurize();
                if (ShouldOpenDoors) _hangarDoors.ForEach(a => a.OpenDoor());
                if (ShouldCloseDoors) _hangarDoors.ForEach(a => a.CloseDoor());
                if (HasClosed) _hangarStatus = HangarStatus.Closed;
                if (HasOpened) _hangarStatus = HangarStatus.Open;
            }

            private void Depressurize()
            {
                _hangarAirVents.ForEach(a => a.Depressurize = true);
                _hangarSideDoors.ForEach(s =>
                {
                    s.CloseDoor();
                    s.Enabled = false;
                });
            }

            private void Pressurize()
            {
                _hangarAirVents.ForEach(a => a.Depressurize = false);
                _hangarSideDoors.ForEach(s =>
                {
                    s.Enabled = true;
                    s.OpenDoor();
                });
            }
        }
    }
}
