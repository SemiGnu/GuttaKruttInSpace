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
    partial class Program : MyGridProgram
    {
        MyIni _ini = new MyIni();
        List<Hangar> _hangars;

        public Program()
        {
            MyIniParseResult result;
            if (!_ini.TryParse(Me.CustomData, out result))
            {
                Echo($"CustomData error:\nLine {result}");
            }
            _hangars = _ini.Get("Hangars", "Names").ToString()
                .Split(',')
                .Select(name => new Hangar(name, GridTerminalSystem))
                .ToList();
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Trigger) > 0)
            {
                _hangars.FirstOrDefault(h => h.Name == argument)?.Toggle();
            }
            if ((updateSource & UpdateType.Update10) > 0)
            {
                _hangars.ForEach(h => h.Main());
            }
        }

        public class Hangar
        {
            List<IMyAirtightHangarDoor> _hangarDoors;
            List<IMyAirVent> _hangarAirVents;
            List<IMyDoor> _hangarSideDoors;
            List<IMyTextSurface> _hangarLcds;
            List<IMyLightingBlock> _hangarLights;
            
            public string Name { get; set; }

            /* 
            public enum DoorStatus
            {
                Opening = 0,    // 0b000000
                Open = 1,       // 0b000001
                Closing = 2,    // 0b000010
                Closed = 3      // 0b000011
            }
            public enum VentStatus << 2
            {
                Depressurized = 0,   // 0b000000
                Depressurizing = 4,  // 0b000100
                Pressurized = 8,     // 0b001000
                Pressurizing = 12    // 0b001100
            }
             */
            public enum HangarStatus
            {
                Opening = 0, Open = 16, Closing = 32, Closed = 48
            }
            int _doorStatus => (int) _hangarDoors.First().Status;
            int _ventStatus => (int) _hangarAirVents.First().Status << 2;
            int _hangarStatus;
            bool ShouldDepressurize => (_hangarStatus & _ventStatus & ~_doorStatus) == 8; // 0b001000
            bool ShouldPressurize => (_hangarStatus & _ventStatus & _doorStatus) == 25; // 0b100011
            bool ShouldOpenDoors => (_hangarStatus & _ventStatus & _doorStatus) == 3; // 0b000011
            bool ShouldCloseDoors => (_hangarStatus & ~_ventStatus & _doorStatus) == 33; // 0b100001
            bool HasOpened => (_hangarStatus & ~_ventStatus & _doorStatus) == 1; // 0b000001
            bool HasClosed => (_hangarStatus & _ventStatus & ~_doorStatus) == 40; // 0b101000
            bool IsIdle => (_hangarStatus & 16) == 16; // 0b010000

            bool IsOpening { 
                get { return _hangarStatus <= 16; } 
                set {
                    if (value && _hangarStatus >= 32) _hangarStatus = (int)HangarStatus.Opening;
                    if (!value && _hangarStatus <= 16) _hangarStatus = (int)HangarStatus.Closing;
                } 
            }

            public Hangar(string name, IMyGridTerminalSystem grid)
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
                IsOpening = !IsOpening;
            }

            void UpdateState()
            {
                if (ShouldDepressurize)
                {
                    _hangarAirVents.ForEach(a => a.Depressurize = true);
                    _hangarSideDoors.ForEach(s =>
                    {
                        s.CloseDoor();
                        s.Enabled = false;
                    });
                }
                if (ShouldPressurize)
                {
                    _hangarAirVents.ForEach(a => a.Depressurize = false);
                    _hangarSideDoors.ForEach(s =>
                    {
                        s.Enabled = true;
                        s.OpenDoor();
                    });
                }
                if (ShouldOpenDoors) _hangarDoors.ForEach(a => a.OpenDoor());
                if (ShouldCloseDoors) _hangarDoors.ForEach(a => a.CloseDoor());
                if (HasClosed) _hangarStatus = (int)HangarStatus.Closed;
                if (HasOpened) _hangarStatus = (int)HangarStatus.Open;
            }
        }
    }
}
