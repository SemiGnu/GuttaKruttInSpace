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
        List<string> _mineralSubtypeIds => new List<string> { "Cobalt", "Gold", "Iron", "Magnesium", "Nickel", "Platinum", "Silicon", "Silver", "Stone", "Uranium" };

        public void UpdateSorters()
        {
            _activeMineral = _minerals.FirstOrDefault(m => m.OreAmount > 0);
            if (_activeMineral == null) return;
            _refineryOut.SetFilter(MyConveyorSorterMode.Blacklist, _activeMineral.OreFilter);
            foreach(var refinery in _refineries)
            {
                refinery.InputInventory.TransferItemTo(_container.GetInventory(), 0, stackIfPossible: true);
            }
            _refineryIn.SetFilter(MyConveyorSorterMode.Whitelist, _activeMineral.OreFilter);
        }

        public void SelectMineral()
        {
            var hover = _minerals.First(m => m.SelectState != SelectState.None);
            hover.SelectState = 1 - hover.SelectState;
        }

        public void MoveMineral(int step)
        {
            var hover = _minerals.First(m => m.SelectState != SelectState.None);
            var index = _minerals.IndexOf(hover);
            var newIndex = index + step;
            if (newIndex < 0 || newIndex >= _minerals.Count()) return;

            if (hover.SelectState == SelectState.Hover)
            {
                _minerals[index].SelectState = SelectState.None;
                _minerals[newIndex].SelectState = SelectState.Hover;
            }

            if (hover.SelectState == SelectState.Selected)
            {
                _minerals.RemoveAt(index);
                _minerals.Insert(newIndex, hover);
            }
        }

        public void InitMinerals()
        {
            var order = Storage ?? "";
            _minerals = _mineralSubtypeIds
                .OrderBy(m => order.IndexOf(m))
                .Select(id => new Mineral(id))
                .ToList();
            _minerals.First().SelectState = SelectState.Hover;
        }

        public void UpdateMineralAmounts()
        {
            foreach(var mineral in _minerals)
            {
                mineral.IngotAmount = _ingotInventory.GetItemAmount(mineral.IngotType);
                mineral.OreAmount = _oreInventories
                    .Select(i => i.GetItemAmount(mineral.OreType))
                    .Aggregate((a,c) => a+c);
            }
        }

        public class Mineral
        {
            public SelectState SelectState { get; set; } = SelectState.None;
            public string OreTypeId => $"MyObjectBuilder_Ore/{MineralSubtypeId}";
            public MyFixedPoint OreAmount { get; set; } = 0;
            public string IngotTypeId => $"MyObjectBuilder_Ingot/{MineralSubtypeId}";
            public MyFixedPoint IngotAmount { get; set; } = 0;
            public string MineralSubtypeId { get; set; }
            public MyItemType IngotType => MyItemType.Parse(IngotTypeId);
            public MyItemType OreType => MyItemType.Parse(OreTypeId);
            public List<MyInventoryItemFilter> OreFilter { get; private set; }
            public Mineral(string subTypeId)
            {
                MineralSubtypeId = subTypeId;
                OreFilter = new List<MyInventoryItemFilter> { new MyInventoryItemFilter(OreTypeId) };
            }
            private string ShowSelectState()
            {
                if (SelectState == SelectState.Selected) return ">";
                if (SelectState == SelectState.Hover) return ":";
                return " ";
            }
            public override string ToString()
            {
                return ShowSelectState() + MineralSubtypeId.PadRight(9) + Display(OreAmount).PadLeft(8) + Display(IngotAmount).PadLeft(8);
            }
        }

        public enum SelectState
        {
            Selected = 0,
            Hover = 1,
            None = 2,
        }
    }
}
