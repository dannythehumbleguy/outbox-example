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

    const res = http.post('http://localhost:5000/api/orders', payload, {
        headers: { 'Content-Type': 'application/json' },
    });

    check(res, {
        'status is 201': (r) => r.status === 201,
    });
}