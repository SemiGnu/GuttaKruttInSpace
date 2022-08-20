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
        List<IMyTextSurface> Lcds { get; set; }
        IMyPistonBase TopPiston;
        IMyShipConnector TopConnector;
        IMyShipMergeBlock TopMergeBlock;
        IMyPistonBase BottomPiston;
        IMyShipConnector BottomConnector;
        IMyShipMergeBlock BottomMergeBlock;
        List<IMyInventory> StoneInventories = new List<IMyInventory>();
        List<IMyInventory> IronInventories = new List<IMyInventory>();
        IMyInventory SiliconInventory;
        IMyInventory NickelInventory;
        IMyInventory GravelInventory;
        List<IMyPistonBase> Pistons = new List<IMyPistonBase>();
        List<IMyFunctionalBlock> MainPower = new List<IMyFunctionalBlock>();
        IMyAssembler Assembler; 
        IMyConveyorSorter DumpSorter; 

        Dictionary<MyDefinitionId, double> Blueprint = new Dictionary<MyDefinitionId, double>
        {
            [MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/InteriorPlate")] = 90,
            [MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ConstructionComponent")] = 130,
            [MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/MotorComponent")] = 36,
            [MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SmallTube")] = 80,
            [MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SteelPlate")] = 125,
        };

        enum State
        {
            Resetting, Mining, ConnectingTop, ConnectingBottom, DisconnectingTop, DisconnectingBottom
        }
        State? _null = null;

        StateMachine<State> _stateMachine;

        const float PistonMineSpeed = 0.02f;
        const float PistonDescendSpeed = -0.3f;

        const float PistonMaxLimit = 8.59f;
        const float PistonMinLimit = 1.09f;
        const float LateralPistonMaxLimit = 7.34f;
        const float LateralPistonMinLimit = 6.34f;
        const float PistonStroke = PistonMaxLimit - PistonMinLimit;
        const float PistonHalfStroke = PistonStroke / 2;

        float PistonPosition => Pistons[0].CurrentPosition - PistonMinLimit;
        float PistonRatio => PistonPosition / PistonStroke;

        bool Active = false;
        bool ActiveOverride = false;


        Dictionary<string, MyInventoryItemFilter> Materials = new Dictionary<string, MyInventoryItemFilter>
        {
            ["gravel"] = new MyInventoryItemFilter(MyDefinitionId.Parse("MyObjectBuilder_Ingot/Stone")),
            ["iron"] = new MyInventoryItemFilter(MyDefinitionId.Parse("MyObjectBuilder_Ingot/Iron")),
            ["silicon"] = new MyInventoryItemFilter(MyDefinitionId.Parse("MyObjectBuilder_Ingot/Silicon")),
            ["nickel"] = new MyInventoryItemFilter(MyDefinitionId.Parse("MyObjectBuilder_Ingot/Nickel"))
        };


        public Program()
        {
            TopPiston = GridTerminalSystem.GetBlockWithName("Deep Core Miner Top Piston") as IMyPistonBase;
            TopConnector = GridTerminalSystem.GetBlockWithName("Deep Core Miner Top Connector") as IMyShipConnector;
            TopMergeBlock = GridTerminalSystem.GetBlockWithName("Deep Core Miner Top Merge Block") as IMyShipMergeBlock;
            BottomPiston = GridTerminalSystem.GetBlockWithName("Deep Core Miner Bottom Piston") as IMyPistonBase;
            BottomConnector = GridTerminalSystem.GetBlockWithName("Deep Core Miner Bottom Connector") as IMyShipConnector;
            BottomMergeBlock = GridTerminalSystem.GetBlockWithName("Deep Core Miner Bottom Merge Block") as IMyShipMergeBlock;
            GridTerminalSystem.GetBlocksOfType(Pistons, p => p.CustomName.StartsWith("Deep Core Miner Piston"));

            GridTerminalSystem.GetBlockGroupWithName("Deep Core Miner Main Power").GetBlocksOfType(MainPower);

            var assemblers = new List<IMyAssembler>();
            GridTerminalSystem.GetBlocksOfType(assemblers);
            Assembler = assemblers.FirstOrDefault(a => !a.CooperativeMode) ?? assemblers.First();

            DumpSorter = GridTerminalSystem.GetBlockWithName("Deep Core Miner Dump Sorter") as IMyConveyorSorter;

            var invetoryOwners = new List<IMyInventoryOwner>();
            GridTerminalSystem.GetBlockGroupWithName("Deep Core Miner Containers").GetBlocksOfType(invetoryOwners);
            StoneInventories = invetoryOwners.Select(i => i.GetInventory(0)).ToList();

            invetoryOwners = new List<IMyInventoryOwner>();
            GridTerminalSystem.GetBlocksOfType(invetoryOwners, c => (c as IMyTerminalBlock).CustomName.StartsWith("Iron Ingot Container"));
            IronInventories = invetoryOwners.Select(i => i.GetInventory(0)).ToList();

            SiliconInventory = (GridTerminalSystem.GetBlockWithName("Silicon Wafer Container") as IMyInventoryOwner).GetInventory(0);
            NickelInventory = (GridTerminalSystem.GetBlockWithName("Nickel Ingot Container") as IMyInventoryOwner).GetInventory(0);
            GravelInventory = (GridTerminalSystem.GetBlockWithName("Gravel Container") as IMyInventoryOwner).GetInventory(0);

            Pistons.ForEach(p =>
            {
                p.MaxLimit = PistonMaxLimit;
                p.MinLimit = PistonMinLimit;
            });
            TopPiston.MaxLimit = BottomPiston.MaxLimit = LateralPistonMaxLimit;
            TopPiston.MinLimit = BottomPiston.MinLimit = LateralPistonMinLimit;
            var states = new[]
            {
                new StateMachineState<State>{
                    Id = State.ConnectingBottom,
                    NextState = () => BottomConnector.Status == MyShipConnectorStatus.Connected ? State.DisconnectingTop : _null,
                    Update = () => {
                        TopPiston.Extend();
                        if (BottomMergeBlock.IsConnected)
                        {
                            BottomConnector.Connect();
                        }
                        return "Connecting bottom";
                    }
                },
                new StateMachineState<State>{
                    Id = State.DisconnectingBottom,
                    NextState = () => BottomPiston.CurrentPosition == BottomPiston.MinLimit ? State.Mining : _null,
                    Update = () => {
                        BottomConnector.Disconnect();
                        BottomMergeBlock.Enabled = false;
                        TopPiston.Retract();
                        return "Disconnecting bottom";
                    }
                },
                new StateMachineState<State>{
                    Id = State.ConnectingTop,
                    NextState = () => TopConnector.Status == MyShipConnectorStatus.Connected ? State.DisconnectingBottom : _null,
                    Update = () => {
                        TopPiston.Extend();
                        if (TopMergeBlock.IsConnected)
                        {
                            QueueBlueprint();
                            TopConnector.Connect();
                        }
                        return "Connecting Top";
                    }
                },
                new StateMachineState<State>{
                    Id = State.DisconnectingTop,
                    NextState = () => TopPiston.CurrentPosition == TopPiston.MinLimit ? State.Resetting : _null,
                    Update = () => {
                        TopConnector.Disconnect();
                        TopMergeBlock.Enabled = false;
                        TopPiston.Retract();
                        return "Disconnecting top";
                    }
                },
                new StateMachineState<State>{
                    Id = State.Mining,
                    NextState = () => Pistons.All(p => p.CurrentPosition == p.MaxLimit) ? State.ConnectingTop : _null,
                    Update = () => {
                        TopMergeBlock.Enabled = true;
                        Pistons.ForEach(p => p.Velocity = PistonMineSpeed);
                        return $"Mining: {PistonRatio:p0}";
                    }
                },
                new StateMachineState<State>{
                    Id = State.Resetting,
                    NextState = () => Pistons.All(p => p.CurrentPosition == p.MinLimit) ? State.ConnectingBottom : _null,
                    Update = () => {
                        BottomMergeBlock.Enabled = true;
                        Pistons.ForEach(p => p.Velocity = PistonDescendSpeed);
                        return $"Resetting: {1-PistonRatio:p0}";
                    }
                },
            };

            var surfaceProviders = new List<IMyTextSurfaceProvider>();
            GridTerminalSystem.GetBlocksOfType(surfaceProviders, s => (s as IMyTerminalBlock)?.CustomName.StartsWith("Deep Core Miner") == true);
            Lcds = surfaceProviders.Select(s => s.GetSurface(0)).ToList();

            State startState;
            startState = Enum.TryParse(Storage, out startState) ? startState : 0;
            _stateMachine = new StateMachine<State>(startState, states);

            Runtime.UpdateFrequency = UpdateFrequency.Update100;

        }

        public void Save()
        {
            Storage = $"{_stateMachine.ActiveState.Id}";
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Trigger) > 0 && argument == "Toggle")
            {
                ActiveOverride = !ActiveOverride;
            }

            SetActive();
            UpdatePower();
            var status = Active ? "Active\n" : "Inactive\n";
            status += _stateMachine.Update();
            status += $"\nInventories: {StoneInventories.Count()}";
            status += $"\nInventory: {GetInventoryRatio("stone"):p0}";
            Lcds.ForEach(l => l?.WriteText(status));
        }

        private void QueueBlueprint()
        {
            foreach (var item in Blueprint)
            {
                Assembler.AddQueueItem(item.Key, item.Value);
            }
        }

        private void SetActive()
        {
            Active =
                !ActiveOverride &&
                GetInventoryRatio("stone") < 0.9f &&
                Materials.Any(m => GetInventoryRatio(m.Key) < 0.75f);

            var filter = Materials.Where(m => GetInventoryRatio(m.Key) > 0.9f).Select(m => m.Value).ToList();
            DumpSorter.SetFilter(MyConveyorSorterMode.Whitelist, filter);
        }

        private void UpdatePower()
        {
            MainPower.ForEach(b => b.Enabled = Active);
        }


        private float GetInventoryRatio(string material)
        {
            float maxInventorySpace, currentInventorySpace;
            switch (material)
            {
                case "stone":
                    maxInventorySpace = StoneInventories.Sum(i => (float)i.MaxVolume);
                    currentInventorySpace = StoneInventories.Sum(i => (float)i.CurrentVolume);
                    break;
                case "iron":
                    maxInventorySpace = IronInventories.Sum(i => (float)i.MaxVolume);
                    currentInventorySpace = IronInventories.Sum(i => (float)i.CurrentVolume);
                    break;
                case "silicon":
                    maxInventorySpace = (float)SiliconInventory.MaxVolume;
                    currentInventorySpace = (float)SiliconInventory.CurrentVolume;
                    break;
                case "gravel":
                    maxInventorySpace = (float)GravelInventory.MaxVolume;
                    currentInventorySpace = (float)GravelInventory.CurrentVolume;
                    break;
                case "nickel":
                    maxInventorySpace = (float)NickelInventory.MaxVolume;
                    currentInventorySpace = (float)NickelInventory.CurrentVolume;
                    break;
                default:
                    throw new Exception("FUCK OFF!");
            }
            return currentInventorySpace / maxInventorySpace;
        }
    }
}
