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
        IMyCargoContainer _container;

        IMyConveyorSorter _refineryIn;
        IMyConveyorSorter _refineryOut;

        IMyTextSurface _refineryLcd;
        IMyTextSurface _ownLcd;

        List<IMyRefinery> _refineries;
        List<IMyInventory> _oreInventories;
        IMyInventory _ingotInventory;

        List<Mineral> _minerals;
        Mineral _activeMineral;


        public Program()
        {
            _container = GridTerminalSystem.GetBlockWithName("Main Container") as IMyCargoContainer;

            _refineries = new List<IMyRefinery>();
            GridTerminalSystem.GetBlocksOfType<IMyRefinery>(_refineries);

            _oreInventories = _refineries.Select(r => r.InputInventory).ToList();
            _oreInventories.Add(_container.GetInventory());
            _ingotInventory = _container.GetInventory();

            _refineryIn = GridTerminalSystem.GetBlockWithName("Refinery In") as IMyConveyorSorter;
            _refineryOut = GridTerminalSystem.GetBlockWithName("Refinery Out") as IMyConveyorSorter;
            _refineryLcd = GridTerminalSystem.GetBlockWithName("Refinery LCD") as IMyTextSurface;
            _ownLcd = Me.GetSurface(0);

            InitMinerals();
            UpdateMineralAmounts();

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Save()
        {
            Storage = string.Join(",", _minerals.Select(m => m.MineralSubtypeId));   
        }

        public void Main(string argument, UpdateType updateSource)
        {
            switch (updateSource)
            {
                case UpdateType.Update10:
                    HandleUpdate();
                    break;
                case UpdateType.Trigger:
                    HandleTrigger(argument);
                    break;
            }
        }

        private void HandleTrigger(string argument)
        {
            Echo(argument);
            switch (argument)
            {
                case "Select":
                    SelectMineral();
                    break;
                case "Up":
                    MoveMineral(-1);
                    UpdateSorters(); 
                    break;
                case "Down":
                    MoveMineral(1);
                    UpdateSorters();
                    break;
            }
        }

        private void HandleUpdate()
        {
            UpdateMineralAmounts();
            OutputInvenoryStatus(_refineryLcd);
            OutputRefineryStatus(_ownLcd);
        }

        private void OutputInvenoryStatus(IMyTextSurface lcd)
        {
            var status = " Mineral       Ore  Ingots\n";
            status += "--------------------------\n";
            status += string.Join("\n", _minerals) + "\n";
            status += "--------------------------\n";
            status += GetRefineryStatus();
            lcd.WriteText(status);
        }

        private void OutputRefineryStatus(IMyTextSurface lcd)
        {
            string status = GetRefineryStatus();
            lcd.WriteText(status);
        }

        private string GetRefineryStatus()
        {
            var refineryLines = _refineries.Select(refinery => $"{refinery.CustomName}: {refinery.InputInventory.GetItemAt(0)?.Type.SubtypeId ?? "Empty"}");
            var status = "Refinery Status\n" + string.Join("\n", refineryLines);
            return status;
        }

        public static string Display(MyFixedPoint point)
        {
            if (point >= 1000)
            {
                var s = point.ToString().Substring(0, 3);
                int m = point.ToIntSafe().ToString().Length % 3;
                if (m != 0) s = s.Insert(m, ".");
                s += point >= 1000000000 ? "B" : point >= 1000000 ? "M" : "K";
                return s;
            }
            return $"{(int)point:0}";
        }
    }
}
