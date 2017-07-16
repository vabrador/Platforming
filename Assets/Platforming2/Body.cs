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
    public float targetGroundHeight;

    private void Reset() {
      rigidbody = GetComponent<Rigidbody>();
      foot = FindObjectOfType<Foot>();
      targetGroundHeight = this.transform.position.y;
    }

    private void Start() {
      OmoTools.PhysicsCallbacks.OnPostPhysics += onPostPhysics;
      targetGroundHeight = this.transform.position.y;
    }

    private void onPostPhysics() {
      //Vector3 curVelocity = rigidbody.velocity;
    }

    private void Update() {
      float verticalInput   = Input.GetAxis("Vertical");
      float horizontalInput = Input.GetAxis("Horizontal");

      // Run input
      Vector3 inputVelocity = (Vector3.forward * verticalInput + Vector3.right * horizontalInput).ClampMagnitude(1F);
      inputVelocity *= runSpeed;

      targetPosition += inputVelocity * Time.deltaTime;

      // Clamp targetPosition's distance from body center
      Vector3 bodyToTarget =  targetPosition - rigidbody.position;
      targetPosition = rigidbody.position + bodyToTarget.ClampMagnitude(1F);
    }

    private void FixedUpdate() {
      //Vector3 targetVelocity = (targetPosition - rigidbody.position) / Time.fixedDeltaTime;

      if (foot.isColliding) {
        targetPosition.y = targetGroundHeight;
      }
      else {
        targetPosition.y = rigidbody.position.y;
      }

      if (foot.isColliding) {
        foot.BeginForces();
        foot.ZeroForces();

        foot.PushToTarget(targetPosition);

        foot.UpdateTargetPrediction(targetPosition);
        foot.PushToPredictedTarget();

        if ((targetPosition - rigidbody.position).y > 0F) foot.PrioritizeGravity();

        foot.ApplyForces();
      }

      if (Input.GetButtonDown("Jump") && foot.isColliding) {
        rigidbody.AddForce(Vector3.up * 8F, ForceMode.Impulse);
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
