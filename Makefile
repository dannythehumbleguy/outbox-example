.PHONY: up

up:
	docker compose up --build -d \
		--scale orders-api=5 \
		--scale payment-api=3