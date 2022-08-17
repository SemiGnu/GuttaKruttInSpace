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
        IMyAssembler Assembler;

        Dictionary<MyDefinitionId, double> Blueprint = new Dictionary<MyDefinitionId, double>
        {
            [MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/InteriorPlate")] = 90,
            [MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ConstructionComponent")] = 130,
            [MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/MotorComponent")] = 36,
            [MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SmallTube")] = 80,
            [MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SteelPlate")] = 125,
        };

        public Program()
        {
            var assemblers = new List<IMyAssembler>();
            GridTerminalSystem.GetBlocksOfType(assemblers);
            Assembler = assemblers.FirstOrDefault(a => !a.CooperativeMode) ?? assemblers.First();
        }

        public void Save()
        {
        }

        public void Main(string argument, UpdateType updateSource)
        {
            //foreach (var item in parts)
            foreach (var item in Blueprint)
            {
                Assembler.AddQueueItem(item.Key, item.Value);
            }

        }

    }
}
