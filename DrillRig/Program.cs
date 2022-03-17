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
        IMyShipConnector _connector;
        IMyConveyorSorter _sorter;
        IMyCargoContainer _container;
        IMyTextSurface _cargoLcd;
        List<MyInventoryItemFilter> _stoneFilter;
        List<MyInventoryItemFilter> _noFilter;
        bool _ejecting;


        public Program()
        {
            _connector = GridTerminalSystem.GetBlockWithName("Drill Connector") as IMyShipConnector;
            _sorter = GridTerminalSystem.GetBlockWithName("Drill Sorter") as IMyConveyorSorter;
            _container = GridTerminalSystem.GetBlockWithName("Drill Cargo") as IMyCargoContainer;

            var cockpit = GridTerminalSystem.GetBlockWithName("Drill Cockpit") as IMyCockpit;
            _cargoLcd = cockpit.GetSurface(2);

            var stone = MyDefinitionId.Parse("MyObjectBuilder_Ore/Stone");
            _stoneFilter = new List<MyInventoryItemFilter> {new MyInventoryItemFilter(stone)};
            _noFilter = new List<MyInventoryItemFilter>();

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            UpdateSorter();
            UpdateCargoLcd();
        }

        private void UpdateSorter()
        {
            if (_connector.ThrowOut && !_ejecting && (_connector.Status == MyShipConnectorStatus.Unconnected))
            {
                _ejecting = true;
                _sorter.DrainAll = true;
                _sorter.SetFilter(MyConveyorSorterMode.Whitelist, _stoneFilter);
            }
            if (!_connector.ThrowOut && _ejecting)
            {
                _ejecting = false;
                _sorter.DrainAll = false;
                _sorter.SetFilter(MyConveyorSorterMode.Blacklist, _noFilter);
            }
        }

        private void UpdateCargoLcd()
        {
            var inv = _container.GetInventory();
            var fillPercentage = (double)inv.CurrentVolume.RawValue / (double)inv.MaxVolume.RawValue;
            var venting = $"{(_ejecting && DateTime.Now.Second % 2 == 0 ? "Ejecting Stone" : "")}";
            var status = $"Cargo Bay {venting}\n\n";
            status += GetFillBar(fillPercentage);

            List<MyInventoryItem> items = new List<MyInventoryItem>();
            inv.GetItems(items);

            status += GetItemLines(items);

            _cargoLcd.WriteText(status, false);
            Echo(status);
        }

        private string GetFillBar(double fill)
        {
            var fills = (int)Math.Round(fill * 20);
            var empties = 20 - fills;
            var fillBar = $"[{new string('█', fills)}{new string(' ', empties)}]\n\n";
            return fillBar;
        }

        private string GetItemLines(List<MyInventoryItem> items)
        {
            var itemLines = items
                .OrderByDescending(i => i.Amount.RawValue)
                .Select(i => {
                    var item = i.Type.SubtypeId;
                    item = item.Substring(0, Math.Min(item.Length, 10));
                    var amount = $"{(double)i.Amount:0}";
                    return item.PadRight(11) + amount.PadLeft(11);
                });

            return string.Join("\n", itemLines) + "\n";
        }

    }
}
