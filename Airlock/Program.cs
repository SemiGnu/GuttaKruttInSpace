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
        IMyDoor _innerDoor;
        IMyDoor _outerDoor;
        IMyAirVent _airlockAirvent;
        IMyTextSurface _airlockLcd;

        bool Depressurized => _airlockAirvent.GetOxygenLevel() < 0.01;

        List<string> IniNames = new List<string> { "innerDoor", "outerDoor", "airlockAirvent", "airlockLcd" };

        public Program()
        {
            var names = GetBlockNames();
            _innerDoor = GridTerminalSystem.GetBlockWithName(names["innerDoor"]) as IMyDoor;
            _outerDoor = GridTerminalSystem.GetBlockWithName(names["outerDoor"]) as IMyDoor;
            _airlockAirvent = GridTerminalSystem.GetBlockWithName(names["airlockAirvent"]) as IMyAirVent;
            var lcd = GridTerminalSystem.GetBlockWithName(names["airlockLcd"]) as IMyTextSurfaceProvider;
            _airlockLcd = lcd.GetSurface(0);

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            SetLockStatus();
            UpdateDisplay();
            CloseOpenDoor(_innerDoor);
            CloseOpenDoor(_outerDoor);
        }

        private void UpdateDisplay()
        {
            var state = GetState();
            _airlockLcd.BackgroundColor = state == "Ready" ? Color.Aquamarine : Color.Red;
            var status = $"Airlock Status\nInner door: {_innerDoor.OpenRatio:p0}\nOuter door: {_outerDoor.OpenRatio:p0}\n";
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
            Echo($"{_outerDoor.OpenRatio} {_outerDoor.OpenRatio < 0.01}");
        }

        private void CloseOpenDoor(IMyDoor door)
        {
            if (door.OpenRatio > 0.99)
            {
                door.ApplyAction("Open_Off");
            }
        }

        Dictionary<string,string> GetBlockNames()
        {
            MyIniParseResult result;
            if (!_ini.TryParse(Me.CustomData, out result))
            {
                Echo($"CustomData error:\nLine {result}");
            }
            return IniNames.ToDictionary(n => n, n => _ini.Get("blockNames", n).ToString());
        }

    }
}
