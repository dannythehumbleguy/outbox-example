import http from 'k6/http';
import { check } from 'k6';

// Problem 1: Lost events without the Outbox Pattern
//
// The producer retries forever while Kafka is down, buffering events in memory.
// When the order service is briefly restarted during the outage, the in-memory
// buffer is wiped — those events are gone even though orders were saved to the DB.

// Send requests over ~1 minutes with 50 concurrent users
export const options = {
    stages: [
        { duration: '5s', target: 50 },   // ramp up
        { duration: '40s', target: 50 },  // sustained load
        { duration: '5s', target: 0 },    // ramp down
    ],
    thresholds: {
        http_req_failed: ['rate<0.01'], // nearly zero failures expected (Kafka issues are silent)
    },
};

export default function () {
    const payload = JSON.stringify({
        goodsName: `Item-${__VU}-${__ITER}`,
        price: 10.00, // fixed price makes verification easy
    });

    const res = http.post('http://localhost:5000/api/orders', payload, {
        headers: { 'Content-Type': 'application/json' },
    });

    check(res, {
        'status is 201': (r) => r.status === 201,
    });
}

// HOW TO RUN
//
// 1. Start the test:
//      k6 run tests/testcase_1.js
//
// 2. docker pause orders-kafka; Start-Sleep 10; docker restart orders-api; docker unpause orders-kafka
//
// VERIFICATION (run after the test completes)
//
// SELECT 'orders' as name, COUNT(*) AS count, SUM(price) AS money FROM orders.orders
// UNION ALL
// SELECT 'payments' as name, COUNT(*) AS count, SUM(amount) AS money FROM payment.payments;
//
//  -> orders count > payments count: events buffered during the outage were lost