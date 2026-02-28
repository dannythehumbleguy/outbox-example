.PHONY: up

up:
	docker compose up --build -d \
		--scale orders-api=10 \
		--scale payment-api=5