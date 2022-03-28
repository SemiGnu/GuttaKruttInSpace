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
        List<IMyInventory> _inventories;
        List<IMyTextSurface> _cockpitLcds;
        IMyTextSurface _turretLcd;
        IMySmallMissileLauncherReload _railgun;


        List<IMyBatteryBlock> _batteries = new List<IMyBatteryBlock>();
        List<IMyGasTank> _o2Tanks = new List<IMyGasTank>();

        double _currentO2 => _o2Tanks.Select(t => t.FilledRatio * t.Capacity).Sum();
        float _maxO2;
        double _currentCharge => _batteries.Select(t => t.CurrentStoredPower).Sum();
        float _maxCharge;

        MyItemType _railgunAmmo;
        MyItemType _autocannonAmmo;

        int _totalRailgunAmmo => _inventories.Select(i => i.GetItemAmount(_railgunAmmo).ToIntSafe()).Sum();
        int _totalAutocannonAmmo => _inventories.Select(i => i.GetItemAmount(_autocannonAmmo).ToIntSafe()).Sum();

        System.Text.RegularExpressions.Regex _numberRegex = new System.Text.RegularExpressions.Regex(@"(?<Num>\d+(\.\d+)?)");
        string _reticle = @"
          |
         ---
       /  |  \
     - | - - | -
       \  |  /
         ---
          |
";


        public Program()
        {
            GridTerminalSystem.GetBlocksOfType(_o2Tanks, t => t.CubeGrid == Me.CubeGrid);
            _maxO2 = _o2Tanks.Select(t => t.Capacity).Sum();

            GridTerminalSystem.GetBlocksOfType(_batteries, t => t.CubeGrid == Me.CubeGrid);
            _maxCharge = _batteries.Select(t => t.MaxStoredPower).Sum();

            var containers = new List<IMyEntity>();
            GridTerminalSystem.GetBlocksOfType(containers, c => c.HasInventory);
            _inventories = containers.Select(c => c.GetInventory()).ToList();

            var cockpits = new List<IMyCockpit>();
            GridTerminalSystem.GetBlocksOfType(cockpits);
            _cockpitLcds = cockpits.Where(c => c.SurfaceCount > 0).Select(c => c.GetSurface(c.IsMainCockpit ? 2 : 2)).ToList();

            var railguns = new List<IMySmallMissileLauncherReload>();
            GridTerminalSystem.GetBlocksOfType(railguns);
            _railgun = railguns.First();

            _railgunAmmo = MyItemType.Parse("MyObjectBuilder_AmmoMagazine/SmallRailgunAmmo");
            _autocannonAmmo = MyItemType.Parse("MyObjectBuilder_AmmoMagazine/AutocannonClip"); 

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            PrintStatus();
        }

        void PrintStatus()
        {
            _cockpitLcds.ForEach(l => l.WriteText(GetStatus()));
        }

        string GetStatus()
        {
            var status = "Raigun charge\n";
            status += GetRailgunCharge();
            status += $"Railgun rounds: {_totalRailgunAmmo}\n";
            status += $"Cannon rounds: {_totalAutocannonAmmo}\n";
            status += $"Batteries: {_currentCharge / _maxCharge:p0}\n";
            status += $"02: {_currentO2 / _maxO2:p0}\n";
            return status;
        }
        string GetRailgunCharge()
        {
            float charge = float.Parse(_numberRegex.Matches(_railgun.DetailedInfo.Split('\n')[1])[0].Value);
            return GetFillBar(charge / 16);
        }

        private string GetFillBar(double fill)
        {
            if (fill == 1) return "[██████ LOADED ██████]\n\n";
            var fills = (int)Math.Round(fill * 20);
            var empties = 20 - fills;
            var fillBar = $"[{new string('█', fills)}{new string(' ', empties)}]\n\n";
            return fillBar;
        }
    }
}
