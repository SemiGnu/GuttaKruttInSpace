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
        IMyTextSurface _lcd;
        List<Airlock> _airlocks;

        public Program()
        {
            MyIniParseResult result;
            if (!_ini.TryParse(Me.CustomData, out result))
            {
                Echo($"CustomData error:\nLine {result}");
            }
            _airlocks = _ini.Get("Airlocks", "Names").ToString()
                .Split(',')
                .Select(name => new Airlock(name, GridTerminalSystem))
                .ToList();
            _lcd = Me.GetSurface(0);

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            _airlocks.ForEach(a => a.Main());
            var status = $"Airlock status:\n{string.Join("\n", _airlocks.Select(a => $"{a.Name} - {a.GetState()}"))}";
            _lcd.WriteText(status);
        }


        public class Airlock
        {
            IMyDoor _innerDoor;
            IMyDoor _outerDoor;
            IMyAirVent _airlockAirvent;
            IMyTextSurface _airlockLcd;

            DateTime _lastOpened = DateTime.MinValue;

            public string Name { get; set; }
            bool Depressurized => _airlockAirvent.GetOxygenLevel() < 0.01;

            public Airlock(string name, IMyGridTerminalSystem grid)
            {
                _innerDoor = grid.GetBlockWithName($"{name} Airlock Inner Door") as IMyDoor;
                _outerDoor = grid.GetBlockWithName($"{name} Airlock Outer Door") as IMyDoor;
                _airlockAirvent = grid.GetBlockWithName($"{name} Airlock Air Vent") as IMyAirVent;
                _airlockLcd = (grid.GetBlockWithName($"{name} Airlock LCD") as IMyTextSurfaceProvider)?.GetSurface(0);
                if (_innerDoor == null) throw new Exception($"Airlock '{name}' has no Inner Door");
                if (_outerDoor == null) throw new Exception($"Airlock '{name}' has no Outer Door");
                if (_airlockAirvent == null) throw new Exception($"Airlock '{name}' has no Air Vent");
                if (_airlockLcd == null) throw new Exception($"Airlock '{name}' has no LCD");
                Name = name;
            }
            public void Main()
            {
                SetLockStatus();
                UpdateDisplay();
                CloseOpenDoor(_innerDoor);
                CloseOpenDoor(_outerDoor);
            }

            private void UpdateDisplay()
            {
                var state = GetState();
                _airlockLcd.BackgroundColor = state == "Ready" ? Color.Navy : Color.OrangeRed;
                var status = $"{Name}\nAirlock Status\nInner door: {_innerDoor.Status}\nOuter door: {_outerDoor.Status}\n";
                status += GetState();
                _airlockLcd.WriteText(status);
            }

            public string GetState()
            {
                if (_outerDoor.OpenRatio > 0.01) return "Cycling";
                if (_airlockAirvent.GetOxygenLevel() > 0.01) return "Depressurizing";
                return "Ready";
            }

            private void SetLockStatus()
            {
                _airlockAirvent.Depressurize = _innerDoor.OpenRatio == 0;
                _outerDoor.Enabled = Depressurized;
                _innerDoor.Enabled = _outerDoor.OpenRatio < 0.01;
            }

            private void CloseOpenDoor(IMyDoor door)
            {
                if(door.Status == DoorStatus.Opening)
                {
                    _lastOpened = DateTime.Now;
                }
                if (DateTime.Now > _lastOpened.AddSeconds(1))
                {
                    door.CloseDoor();
                }
            }
        }

    }
}
