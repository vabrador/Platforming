using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides a Vector3 whose point lies within the unit sphere.
/// </summary>
public interface IDirectionProvider {

	Vector3 GetDirection();

}