import http from 'k6/http';
import { check } from 'k6';

// Start the test: k6 run tests/testcase_1.js
export const options = {
    scenarios: {
        constant_rps: {
            executor: 'constant-arrival-rate',
            rate: 300,
            timeUnit: '1s',
            duration: '5m',
            preAllocatedVUs: 100,
            maxVUs: 200,
        },
    },
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