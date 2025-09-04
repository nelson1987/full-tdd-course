import http from "k6/http";
import { check, fail, sleep } from "k6";

// Test configuration
export const options = {
  thresholds: {
    // Assert that 99% of requests finish within 3000ms.
    http_req_duration: ["p(99) < 250"],
  },
  // Ramp the number of virtual users up and down
  stages: [
    { duration: "5s", target: 1000 },
    // { duration: "10s", target: 10 },
    // { duration: "20s", target: 20 },
    // { duration: "30s", target: 30 },
    // { duration: "10s", target: 50 },
    // { duration: "20s", target: 100 },
    // { duration: "30s", target: 200 },
    // { duration: "1s", target: 100 },
    // { duration: "10s", target: 500 },
    // { duration: "1m", target: 2000 },
    // { duration: "20s", target: 3000 },
    // { duration: "30s", target: 3000 },
  ],
};

// Simulated user behavior
export default function () {
  let res = http.get("http://localhost:5000/health");
  // Validate response status
  check(res, { "status was 200": (r) => r.status == 200 });
  // Validate max duration
  if(
    !check(res, { "max duration was 250ms": (r) => r.timings.duration < 250 })
  ){
    fail("Max duration was not met");
  };

  sleep(1);
}
