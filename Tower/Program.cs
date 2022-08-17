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
        List<IMyPistonBase> Pistons = new List<IMyPistonBase>();
        IMyAssembler Assembler;
        IMyShipController Helm;

        Dictionary<MyDefinitionId, double> Blueprint = new Dictionary<MyDefinitionId, double>
        {
            [MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/InteriorPlate")] = 166,
            [MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ConstructionComponent")] = 240,
            [MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/MotorComponent")] = 66,
            [MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SmallTube")] = 148,
            [MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SteelPlate")] = 775,
        };

        FiniteStateMachine StateMachine;


        const float PistonMaxLimit = 9.84f;
        const float PistonMinLimit = 0.67f;
        const float LateralPistonMaxLimit = 2.34f;
        const float LateralPistonMinLimit = 0f;

        const float PistonStroke = PistonMaxLimit - PistonMinLimit;

        float PistonPosition => Pistons[0].CurrentPosition - PistonMinLimit;
        float PistonRatio => PistonPosition / PistonStroke;
        Func<float, float> VerticalSpeed = (x) => -0.224f * x * x + 2.05f * x + 0.3f;
        Func<float, float> LateralSpeed = (x) => -1.97f * x * x + 4.62f * x + 0.3f;

        int GetQueueLength() {
            var list = new List<MyProductionItem>();
            Assembler.GetQueue(list);
            return  list.Sum(l => l.Amount.ToIntSafe());
        }

        bool Active = true;

        public Program()
        {
            Helm = GridTerminalSystem.GetBlockWithName("Tower Helm") as IMyShipController;

            TopPiston = GridTerminalSystem.GetBlockWithName("Tower Top Piston") as IMyPistonBase;
            TopConnector = GridTerminalSystem.GetBlockWithName("Tower Top Connector") as IMyShipConnector;
            TopMergeBlock = GridTerminalSystem.GetBlockWithName("Tower Top Merge Block") as IMyShipMergeBlock;
            BottomPiston = GridTerminalSystem.GetBlockWithName("Tower Bottom Piston") as IMyPistonBase;
            BottomConnector = GridTerminalSystem.GetBlockWithName("Tower Bottom Connector") as IMyShipConnector;
            BottomMergeBlock = GridTerminalSystem.GetBlockWithName("Tower Bottom Merge Block") as IMyShipMergeBlock;
            GridTerminalSystem.GetBlocksOfType(Pistons, p => p.CustomName.StartsWith("Tower Piston"));

            var assemblers = new List<IMyAssembler>();
            GridTerminalSystem.GetBlocksOfType(assemblers);
            Assembler = assemblers.FirstOrDefault(a => !a.CooperativeMode) ?? assemblers.First();

            Pistons.ForEach(p =>
            {
                p.MaxLimit = PistonMaxLimit;
                p.MinLimit = PistonMinLimit;
            });
            TopPiston.MaxLimit = BottomPiston.MaxLimit = LateralPistonMaxLimit;
            TopPiston.MinLimit = BottomPiston.MinLimit = LateralPistonMinLimit;

            var states = new[]
            {
                new State {
                    EndCondition = () => BottomPiston.CurrentPosition == BottomPiston.MinLimit,
                    NextState = 1,
                    Update = () => {
                        BottomConnector.Disconnect();
                        BottomMergeBlock.Enabled = false;
                        BottomPiston.Velocity = -LateralSpeed(BottomPiston.CurrentPosition);
                        return "Disconnecting bottom";
                    }
                },
                new State {
                    EndCondition = () => Pistons.All(p => p.CurrentPosition == p.MinLimit),
                    NextState = 2,
                    Update = () => {
                        BottomMergeBlock.Enabled = true;
                        var speed = VerticalSpeed(PistonPosition);
                        Pistons.ForEach(p => p.Velocity = -speed);
                        return $"Grinding: {1-PistonRatio:p0}";
                    }
                },
                new State {
                    EndCondition = () => BottomConnector.Status == MyShipConnectorStatus.Connected,
                    NextState = 3,
                    Update = () => {
                        BottomPiston.Velocity = LateralSpeed(BottomPiston.CurrentPosition);
                        if (BottomMergeBlock.IsConnected)
                        {
                            BottomConnector.Connect();
                        }
                        return "Connecting bottom";
                    }
                },
                new State {
                    EndCondition = () => TopPiston.CurrentPosition == TopPiston.MinLimit,
                    NextState = 4,
                    Update = () => {
                        TopConnector.Disconnect();
                        TopMergeBlock.Enabled = false;
                        TopPiston.Velocity = -LateralSpeed(TopPiston.CurrentPosition);
                        return "Disconnecting top";
                    }
                },
                new State {
                    EndCondition = () => Pistons.All(p => p.CurrentPosition == p.MaxLimit) && GetQueueLength() == 0,
                    NextState = 5,
                    Update = () => {
                        TopMergeBlock.Enabled = true;
                        var speed = 0.3f;// VerticalSpeed(PistonPosition);
                        Pistons.ForEach(p => p.Velocity = speed);
                        return $"Building: {PistonRatio:p0}";
                    }
                },
                new State {
                    EndCondition = () => TopConnector.Status == MyShipConnectorStatus.Connected,
                    NextState = 0,
                    Update = () => {
                        TopPiston.Velocity = LateralSpeed(TopPiston.CurrentPosition);
                        if (TopMergeBlock.IsConnected)
                        {
                            QueueBlueprint();
                            TopConnector.Connect();
                        }
                        return "Connecting Top";
                    }
                },
            };

            var surfaceProviders = new List<IMyTextSurfaceProvider>();
            GridTerminalSystem.GetBlocksOfType(surfaceProviders, s => (s as IMyTerminalBlock)?.CustomName.StartsWith("Tower") == true);
            Lcds = surfaceProviders.Select(s => s.GetSurface(0)).ToList();

            int startIndex;
            startIndex = int.TryParse(Storage, out startIndex) ? startIndex : 0;
            StateMachine = new FiniteStateMachine(startIndex, states);

            Runtime.UpdateFrequency = UpdateFrequency.Update10;

        }

        public void Save()
        {
            Storage = $"{StateMachine.ActiveStateIndex}";
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Trigger) > 0 && argument == "Toggle")
            {
                Active = !Active;
            }
            UpdatePower();
            var status = Active ? "Active" : "Inactive";
            status += $"\n Gravity: - {Helm.GetNaturalGravity().Length()/9.81:0.00}\n";
            status += $"Assembler queue: {GetQueueLength()}\n";
            if (Helm.GetNaturalGravity().Length() > 0)
            {
                status += StateMachine.Update();
            } 
            Lcds.ForEach(l => l?.WriteText(status));
        }

        private void QueueBlueprint()
        {
            foreach (var item in Blueprint)
            {
                Assembler.AddQueueItem(item.Key, item.Value);
            }
        }

        private void UpdatePower()
        {
            Pistons.ForEach(b => b.Enabled = Active);
        }

    }
}
