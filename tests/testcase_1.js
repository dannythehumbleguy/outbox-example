import http from 'k6/http';
import { check } from 'k6';

// Start the test: k6 run tests/testcase_1.js
export const options = {
    stages: [
        { duration: '5s', target: 50 },
        { duration: '40s', target: 50 },
        { duration: '5s', target: 0 },
    ],
    thresholds: {
        http_req_failed: ['rate<0.01'],
    },
};

export default function () {
    const payload = JSON.stringify({
        goodsName: `Item-${__VU}-${__ITER}`,
        price: 10.00,
    });

    const res = http.post('http://localhost/orders/api/orders', payload, {
        headers: { 'Content-Type': 'application/json' },
    });

    check(res, {
        'status is 201': (r) => r.status === 201,
    });
}

// HOW TO RUN
//
// 1. Start the test:
//      k6 run tests/problem_1.js
//
// 2. docker pause orders-kafka; Start-Sleep 10; docker restart orders-api; docker unpause orders-kafka
//
// VERIFICATION (run after the test completes)
//
//  SELECT COUNT(*) AS count, SUM(price) AS money FROM orders.orders
//  UNION ALL
//  SELECT COUNT(*) AS count, SUM(amount) AS money FROM payment.payments;
//
//  -> orders count > payments count: events buffered during the outage were lost