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
		#region PID Class

		/// <summary>
		/// Discrete time PID controller class.
		/// (Whiplash141 - 11/22/2018)
		/// </summary>
		public class PID
		{
			readonly double _kP = 0;
			readonly double _kI = 0;
			readonly double _kD = 0;

			double _timeStep = 0;
			double _inverseTimeStep = 0;
			double _errorSum = 0;
			double _lastError = 0;
			bool _firstRun = true;

			public double Value { get; private set; }

			public PID(double kP, double kI, double kD, double timeStep)
			{
				_kP = kP;
				_kI = kI;
				_kD = kD;
				_timeStep = timeStep;
				_inverseTimeStep = 1 / _timeStep;
			}

			protected virtual double GetIntegral(double currentError, double errorSum, double timeStep)
			{
				return errorSum + currentError * timeStep;
			}

			public double Control(double error)
			{
				//Compute derivative term
				var errorDerivative = (error - _lastError) * _inverseTimeStep;

				if (_firstRun)
				{
					errorDerivative = 0;
					_firstRun = false;
				}

				//Get error sum
				_errorSum = GetIntegral(error, _errorSum, _timeStep);

				//Store this error as last error
				_lastError = error;

				//Construct output
				this.Value = _kP * error + _kI * _errorSum + _kD * errorDerivative;
				return this.Value;
			}

			public double Control(double error, double timeStep)
			{
				if (timeStep != _timeStep)
				{
					_timeStep = timeStep;
					_inverseTimeStep = 1 / _timeStep;
				}
				return Control(error);
			}

			public void Reset()
			{
				_errorSum = 0;
				_lastError = 0;
				_firstRun = true;
			}
		}

		#endregion
	}
}
