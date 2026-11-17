# Run commands from deploy/, or use make -C deploy from the repository root.
SHELL := /bin/bash
.SHELLFLAGS := -eu -o pipefail -c
.NOTPARALLEL:

-include .env
PYTHON ?= python3

export AWS_PROFILE AWS_REGION ANSIBLE_PRIVATE_KEY_FILE
export TF_VAR_allowed_client_cidr
export ORDERS_REPLICAS PAYMENT_REPLICAS ORDERS_DB_PASSWORD PAYMENT_DB_PASSWORD GRAFANA_ADMIN_PASSWORD
export ANSIBLE_CONFIG := ansible/ansible.cfg

.PHONY: setup validate outputs provision configure check-image-tag build deploy scale

setup:
	$(PYTHON) -m venv .venv
	.venv/bin/pip install -r ansible/requirements.txt
	.venv/bin/ansible-galaxy collection install -r ansible/requirements.yml
	.venv/bin/ansible-playbook -i localhost, ansible/setup.yml

validate:
	terraform -chdir=terraform fmt -check -recursive
	terraform -chdir=terraform validate
	terraform -chdir=terraform test
	.venv/bin/ansible-playbook -i localhost, --syntax-check ansible/provision.yml
	.venv/bin/ansible-playbook -i localhost, --syntax-check ansible/deploy.yml
	.venv/bin/ansible-playbook -i localhost, --syntax-check ansible/scale.yml
	.venv/bin/ansible-playbook -i localhost, tests/validate.yml
	.venv/bin/python -m unittest discover -s tests -p 'test_*.py'

outputs:
	@mkdir -p generated
	terraform -chdir=terraform output -json inventory > generated/inventory.json.tmp
	terraform -chdir=terraform output -json deployment > generated/deployment.json.tmp
	@mv generated/inventory.json.tmp generated/inventory.json
	@mv generated/deployment.json.tmp generated/deployment.json

configure: outputs
	.venv/bin/ansible-playbook -i generated/inventory.json -e @generated/deployment.json ansible/provision.yml

check-image-tag:
	@test -n "$(IMAGE_TAG)" || { echo 'Set IMAGE_TAG, for example: make build IMAGE_TAG=test-001' >&2; exit 1; }

# Build pushes images; deploy uses those existing images and never builds.
build: check-image-tag outputs
	aws ecr get-login-password --region "$$(jq -er .aws_region generated/deployment.json)" | \
		docker login --username AWS --password-stdin "$$(jq -er .ecr_registry generated/deployment.json)"
	docker buildx build --platform linux/arm64 --push \
		-f ../src/OrderService/OrderService.Api/Dockerfile \
		-t "$$(jq -er .orders_repository generated/deployment.json):$(IMAGE_TAG)" ../src/OrderService
	docker buildx build --platform linux/arm64 --push \
		-f ../src/PaymentService/PaymentService.Api/Dockerfile \
		-t "$$(jq -er .payment_repository generated/deployment.json):$(IMAGE_TAG)" ../src/PaymentService

deploy: check-image-tag outputs
	.venv/bin/ansible-playbook -i localhost, -e @generated/deployment.json -e "image_tag=$(IMAGE_TAG)" ansible/deploy.yml

scale: outputs
	.venv/bin/ansible-playbook -i localhost, -e @generated/deployment.json ansible/scale.yml

