ORDERS_REPLICAS ?= 10
PAYMENT_REPLICAS ?= 5
BASE_URL ?= http://localhost

.PHONY: up load-test

up:
	docker compose up --build -d \
		--scale orders-api=$(ORDERS_REPLICAS) \
		--scale payment-api=$(PAYMENT_REPLICAS)

load-test:
	k6 run -e "BASE_URL=$(BASE_URL)" tests/testcase_1.js
