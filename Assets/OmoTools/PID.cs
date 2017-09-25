using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPID<T> {
	float Kp { get; }
	float Ki { get; }
	float Kd { get; }

	T target { get; }
	T output { get; }

	T Update(T measuredValue, float deltaTime);

	void SetConstants(float kp, float ki, float kd);
}

public struct PID : IPID<float> {

	public float Kp { get; set; }
	public float Ki { get; set; }
	public float Kd { get; set; }
	
	public float target { get; set; }
	public float output { get; set; }

	public float prevErr;
	public float integral;

	public PID(float target) {
		Kp = 1f;
		Ki = 1f;
		Kd = 1f;

		this.target = target;
		this.output = default(float);

		prevErr = 0f;
		integral = 0f;
	}

	public float Update(float measuredValue, float deltaTime) {
		float err = target - measuredValue;
		integral = integral + err * deltaTime;
		var derivative = (err - prevErr) / deltaTime;

		var output = Kp * err + Ki * integral + Kd * derivative;
		
		prevErr = err;

		this.output = output;
		return output;
	}

	public void SetConstants(float kp, float ki, float kd) {
		Kp = kp;
		Ki = ki;
		Kd = kd;
	}

}

public struct VectorPID : IPID<Vector3> {

	public float Kp {
		get { return _xPID.Kp; }
		set { SetConstants(value, Ki, Kd); }
	}
	public float Ki {
		get { return _xPID.Ki; }
		set { SetConstants(Kp, value, Kd); }
	}
	public float Kd {
		get { return _xPID.Kd; }
		set { SetConstants(Kp, Ki, value); }
	}
	
	public Vector3 target {
		get {
			return new Vector3(_xPID.target,
											   _yPID.target,
												 _zPID.target);
		}
		set {
			_xPID.target = value.x;
			_yPID.target = value.y;
			_zPID.target = value.z;
		}
	}
	public Vector3 output {
		get {
			return new Vector3(_xPID.output,
												 _yPID.output,
												 _zPID.output);
		}
		set {
			_xPID.output = value.x;
			_yPID.output = value.y;
			_zPID.output = value.z;
		}
	}
	
	private PID _xPID;
	private PID _yPID;
	private PID _zPID;

	public VectorPID(Vector3 target) {
		_xPID = new PID(target.x);
		_yPID = new PID(target.y);
		_zPID = new PID(target.z);

		SetConstants(1f, 1f, 1f);
	}

	public Vector3 Update(Vector3 measuredValue, float deltaTime) {
		Vector3 newOutput;
		newOutput.x = _xPID.Update(measuredValue.x, deltaTime);
		newOutput.y = _yPID.Update(measuredValue.y, deltaTime);
		newOutput.z = _zPID.Update(measuredValue.z, deltaTime);
		return newOutput;
	}

	public void SetConstants(float kp, float ki, float kd) {
		_xPID.SetConstants(kp, ki, kd);
		_yPID.SetConstants(kp, ki, kd);
		_zPID.SetConstants(kp, ki, kd);
	}

}