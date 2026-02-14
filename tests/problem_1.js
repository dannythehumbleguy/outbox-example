import http from 'k6/http';
import { check } from 'k6';

// Problem 1: Lost events without the Outbox Pattern
//
// This test demonstrates that when Kafka becomes unavailable, orders are saved
// to the database but events are silently lost — payments are never created.

// Send 100k requests over ~2 minutes with 50 concurrent users
export const options = {
    stages: [
        { duration: '10s', target: 50 },   // ramp up
        { duration: '100s', target: 50 },  // sustained load
        { duration: '10s', target: 0 },    // ramp down
    ],
    thresholds: {
        http_req_failed: ['rate<0.5'], // some failures expected during disconnect
    },
};

// Test 
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
// 1. Start the K6 test:
//  k6 run tests/problem_1.js
//
// 2. ~30 seconds into the test, pause Kafka:
//      docker pause orders-kafka
//
// 3. ~30 seconds later, unpause Kafka:
//      docker unpause orders-kafka
//
// VERIFICATION (run after the test completes)
//
//  SELECT COUNT(*) AS count, SUM(price) AS money FROM orders.orders
//  UNION ALL
//  SELECT COUNT(*) AS count, SUM(amount) AS money FROM payment.payments;
// 
// 4. Clear all data:
//  TRUNCATE orders.orders, payment.payments;
