import http from "k6/http";
import { check, sleep } from "k6";

// Test configuration
export const options = {
  thresholds: {
    // Assert that 99% of requests finish within 3000ms.
    http_req_duration: ["p(99) < 250"],
  },
  // Ramp the number of virtual users up and down
  stages: [
    { duration: "5s", target: 1000 },
  ],
};

// Simulated user behavior
export default function () {
  let res = http.get("http://localhost:5000/weatherforecast");
  // Validate response status
  check(res, { "status was 200": (r) => r.status == 200 });
  if(
    check(res, { "max duration was 250ms": (r) => r.timings.duration < 250 })
  ){
    fail("Max duration was not met");
  };
  
  sleep(1);
}
