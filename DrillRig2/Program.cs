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
        List<IMyInventoryOwner> _containers = new List<IMyInventoryOwner>();
        List<IMyGasTank> _o2Tanks = new List<IMyGasTank>();
        List<IMyTextSurface> _cargoLcds = new List<IMyTextSurface>();

        float _currentCargo => _containers.Select(c => (float) c.GetInventory(0).CurrentVolume).Sum();
        float _maxCargo;

        double _currentO2 => _o2Tanks.Select(t => t.FilledRatio * t.Capacity).Sum();
        float _maxO2;

        List<MyInventoryItem> _allItems = new List<MyInventoryItem>();



        public Program()
        {
            GridTerminalSystem.GetBlocksOfType(_containers, c => ((IMyCubeBlock)c).CubeGrid == Me.CubeGrid && !(c is IMyPowerProducer) && !(c is IMyGasTank));
            GridTerminalSystem.GetBlocksOfType(_o2Tanks, t => t.CubeGrid == Me.CubeGrid);

            

            _maxCargo = _containers.Select(c => (float)c.GetInventory(0).MaxVolume).Sum();
            _maxO2 = _o2Tanks.Select(t => t.Capacity).Sum();

            var surfaceProviders = new List<IMyTextSurfaceProvider>();
            GridTerminalSystem.GetBlocksOfType(surfaceProviders, sp => ((IMyTerminalBlock)sp).CustomData.Contains("[CargoComputer]"));
            foreach (var sp in surfaceProviders)
            {
                var surfaceIndex = CustomDataHelper.GetInt(((IMyTerminalBlock)sp).CustomData, "CargoComputer", "Surface");
                _cargoLcds.Add(sp.GetSurface(surfaceIndex));
            }

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            //Echo($"{_currentCargo}\n{_maxCargo}");
            UpdateCargoLcd();
        }

        private void UpdateCargoLcd()
        {
            var status = $"Cargo Bay - O2:{$"{_currentO2 / _maxO2:p0}".PadLeft(7)}\n\n";
            status += GetFillBar(_currentCargo / _maxCargo);
            status += GetItemLines();
            Echo(status);
            _cargoLcds.ForEach(l => l.WriteText(status));
        }


        private string GetFillBar(double fill)
        {
            var fills = (int)Math.Round(fill * 20);
            var empties = 20 - fills;
            var fillBar = $"[{new string('█', fills)}{new string(' ', empties)}]\n\n";
            return fillBar;
        }

        private string GetItemLines()
        {
            _allItems = new List<MyInventoryItem>();
            foreach(var container in _containers)
            {
                container.GetInventory(0).GetItems(_allItems);
            }

            var groupedItems = _allItems.GroupBy(i => i.Type).ToDictionary(k => k.Key, v => v.Select(i => i.Amount.ToIntSafe()).Sum());
            var itemLines = groupedItems
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => {
                    var item = kvp.Key.SubtypeId;
                    item = item.Substring(0, Math.Min(item.Length, 10));
                    var amount = $"{kvp.Value}";
                    return item.PadRight(11) + amount.PadLeft(11);
                });

            return string.Join("\n", itemLines) + "\n";
        }

    }
}
