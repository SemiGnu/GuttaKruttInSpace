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
        List<IMyJumpDrive> _jumpDrives = new List<IMyJumpDrive>();
        IMyTextSurface _jumpLcd;

        decimal _maxPower;
        decimal _totalPower;

        const decimal JumpdrivePower = 3e6M;

        public Program()
        {
            GridTerminalSystem.GetBlocksOfType(_jumpDrives);
            _maxPower = _jumpDrives.Count() * JumpdrivePower;
            _jumpLcd = (GridTerminalSystem.GetBlockWithName("LCD Jump") as IMyTextSurfaceProvider).GetSurface(0);
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        void Main()
        {
            _totalPower = _jumpDrives.Select(GetPowerFromJumpdrive).Sum();
            PrintStatus();
        }

        private void PrintStatus()
        {
            var status = $"Total Amount \n of Jump Drives: {_jumpDrives.Count}\n\n";
            status += $"Current Power: {_totalPower:0} Wh\n\n";
            status += $"Maximum Power: {_maxPower:0} Wh\n\n";
            status += $"Charge: {_totalPower / _maxPower:p0}";
            _jumpLcd.WriteText(status);
        }

        private decimal GetPowerFromJumpdrive(IMyJumpDrive jumpDrive)
        {
            string thirdrow = jumpDrive.DetailedInfo.ToString().Split('\n')[4];
            return GetWattHours(thirdrow);
        }

        static decimal GetWattHours(string strValue)
        {
            const string regExpr = @"(?<Num>[0-9.]+) (?<Unit>[a-zA-Z]+)";
            var match = System.Text.RegularExpressions.Regex.Match(strValue, regExpr);
            if (!match.Success)
                throw new Exception("Input has an invalid format");
            return decimal.Parse(match.Groups["Num"].Value) * UnitToDecimal(match.Groups["Unit"].Value);
        }

        static decimal UnitToDecimal(string unit)
        {
            if (unit.StartsWith("k")) 
                return 1e3M;
            if (unit.StartsWith("M"))
                return 1e6M;
            return 0;
        }
    }
}
