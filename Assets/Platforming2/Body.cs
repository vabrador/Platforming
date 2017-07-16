using OmoTools;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Platforming2 {

  [RequireComponent(typeof(Rigidbody))]
  public class Body : MonoBehaviour {

    public Rigidbody rigidbody;
    public float runSpeed = 7F;
    public Foot foot;

    [Header("Auto")]
    public Vector3 targetPosition;

    private void Reset() {
      rigidbody = GetComponent<Rigidbody>();
      foot = FindObjectOfType<Foot>();
    }

    private void Start() {
      OmoTools.PhysicsCallbacks.OnPostPhysics += onPostPhysics;
    }

    private void onPostPhysics() {
      //Vector3 curVelocity = rigidbody.velocity;
    }

    private void Update() {
      float verticalInput   = Input.GetAxis("Vertical");
      float horizontalInput = Input.GetAxis("Horizontal");

      // Run input
      Vector3 inputVelocity = Vector3.forward * verticalInput + Vector3.right * horizontalInput;
      inputVelocity *= runSpeed;

      targetPosition += inputVelocity * Time.deltaTime;
    }

    private void FixedUpdate() {
      //Vector3 targetVelocity = (targetPosition - rigidbody.position) / Time.fixedDeltaTime;

      if (foot.isColliding) {
        foot.BeginForces();
        foot.ZeroForces();
        foot.PushToTarget(targetPosition);
        if ((targetPosition - rigidbody.position).y > 0F) foot.PrioritizeGravity();
        foot.ApplyForces();
      }
    }

    private void OnDrawGizmos() {
      Gizmos.color = Color.red;
      float radius = 0.1F;
      float radiusStep = 0.1F;
      int numSteps = 5;
      for (int i = 0; i < numSteps; i++) {
        Gizmos.color = Gizmos.color.WithAlpha((float)(numSteps - i) / (float)numSteps);
        Gizmos.DrawWireSphere(targetPosition, radius);
        radius += radiusStep;
      }
    }

  }


}
