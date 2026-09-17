Developer's Choice — Kubernetes Voting Platform

A containerized, event-driven voting platform built as three independent services: a Flask voting service, a .NET 8 background worker, and a Node.js live-results service. Votes are buffered through Redis, persisted to PostgreSQL, and exposed through a real-time Socket.IO results page.

The repository also demonstrates Docker Compose, Kubernetes, persistent storage, Ingress/TLS, Prometheus monitoring, alerting, and GitHub Actions-based deployment to AKS/ACR.

Architecture

flowchart LR
    U[Browser]
    V[Vote Service<br/>Flask :5000]
    R[(Redis :6379)]
    W[Worker<br/>.NET 8]
    P[(PostgreSQL :5432)]
    S[Result Service<br/>Node.js :4000]
    I[NGINX Ingress]
    M[Prometheus]

    U -->|Submit vote| V
    V -->|RPUSH votes| R
    R -->|LPOP votes| W
    W -->|INSERT / UPDATE| P
    S -->|SQL aggregation| P
    S -->|Socket.IO scores| U
    U -->|HTTPS| I
    I --> V
    I --> S
    S -->|/metrics| M

Request and data flow

The Flask service renders the voting page and assigns a voter_id cookie when needed.

A submitted vote is serialized as JSON and pushed to the Redis votes list.

The .NET worker continuously consumes the list with LPOP.

The worker stores the vote in PostgreSQL using the voter ID as a unique identifier.

If that voter already exists, the worker updates the existing record.

The Node.js result service aggregates PostgreSQL records with GROUP BY vote.

Socket.IO broadcasts the current counts to connected browsers every second.

The result UI converts the counts into live percentage bars.

Important: the vote service queues a vote in Redis before durable PostgreSQL processing occurs. Persistence is intentionally asynchronous.

Key Features

Flask-based voting UI with configurable voting options.

Redis-backed asynchronous vote queue.

Dedicated .NET 8 worker for persistence processing.

PostgreSQL-backed durable vote storage.

Per-browser voter_id used to update an existing vote.

Live result updates through Socket.IO.

Prometheus metrics exposed at /metrics.

Kubernetes Deployments for application services.

PostgreSQL StatefulSet with a 1 GiB PVC.

Kubernetes Services for internal service discovery.

NGINX Ingress with separate vote/result hostnames.

cert-manager ACME configuration for TLS.

Readiness/liveness probes for vote and result services.

Prometheus ServiceMonitor and worker availability alert.

GitHub Actions build → ACR push → AKS deployment workflow.

Commit-SHA image tagging in CI/CD for traceable releases.

Tech Stack

Area

Technology

Vote service

Python 3.11, Flask, Redis client

Worker

.NET 8, Npgsql, StackExchange.Redis, Newtonsoft.Json

Result service

Node.js 20, Express, PostgreSQL (pg), Socket.IO

Data

Redis 7, PostgreSQL 15

Containers

Docker, Docker Compose

Orchestration

Kubernetes

Ingress/TLS

NGINX Ingress, cert-manager, Let's Encrypt ACME

Observability

Prometheus metrics, ServiceMonitor, PrometheusRule

Cloud deployment

Azure Kubernetes Service (AKS), Azure Container Registry (ACR)

CI/CD

GitHub Actions

Repository Structure

Voting_app-main/
├── .github/workflows/deploy.yaml
├── docker-compose.yml
├── vote/
│   ├── app.py
│   ├── Dockerfile
│   ├── requirements.txt
│   └── templates/index.html
├── worker/
│   ├── Program.cs
│   ├── worker.csproj
│   └── dockerfile
├── result/
│   ├── server.js
│   ├── package.json
│   ├── package-lock.json
│   ├── Dockerfile
│   └── public/index.html
└── k8s-specifications/
    ├── vote-deployment.yaml / vote-service.yaml
    ├── worker-deployment.yaml / worker-alert.yaml
    ├── result-deployment.yaml / result-service.yaml / result-servicemonitor.yaml
    ├── redis-deployment.yaml / redis-service.yaml
    ├── postgres-statefulset.yaml / postgres-service.yaml / postgres-secret.yaml
    ├── configmap.yaml
    ├── ingress.yaml
    └── cluster-issuer-prod.yaml

Run Locally with Docker Compose

Prerequisites

Docker with Docker Compose

Start

git clone <repository-url>
cd Voting_app-main
docker compose up --build

The local stack exposes:

Service

Address

Purpose

Vote

http://localhost:5000

Submit votes

Result

http://localhost:4000

View live results

Redis

localhost:6379

Vote queue

PostgreSQL

localhost:5432

Persistent vote storage

Stop the stack with:

docker compose down

The Compose configuration uses Redis 7 and PostgreSQL 15 and wires the application services together through Compose service names (redis and postgres).

Service Responsibilities

Vote — Flask

vote/app.py handles the browser-facing voting workflow. It reads VOTING_OPTIONS, creates a voter_id cookie, and pushes JSON vote messages to Redis.

Worker — .NET 8

worker/Program.cs acts as the consumer. It retries connections while Redis/PostgreSQL are unavailable, consumes votes from Redis, creates the votes table when needed, and performs an insert-or-update operation for each voter.

Result — Node.js

result/server.js serves the results UI, exposes /options and /metrics, queries PostgreSQL for vote counts, and broadcasts scores through Socket.IO.

Kubernetes Deployment

The k8s-specifications/ directory contains the Kubernetes resources required by the application.

Resource

Purpose

Deployments

Run vote, worker, result, and Redis workloads

StatefulSet

Runs PostgreSQL with persistent storage

Services

Provide stable internal networking

ConfigMap

Stores VOTING_OPTIONS

Secret

Supplies PostgreSQL credentials

Ingress

Routes external traffic to vote/result services

ClusterIssuer

Configures Let's Encrypt ACME TLS issuance

ServiceMonitor

Scrapes result-service metrics

PrometheusRule

Alerts when the worker has no available replica

Apply manifests

kubectl apply -f k8s-specifications/

Verify the workloads:

kubectl get pods
kubectl get deployments
kubectl get services
kubectl get ingress

The application manifests currently configure one replica for each application workload and Redis. PostgreSQL is a single-replica StatefulSet with a 1 GiB persistent volume claim.

Kubernetes networking

The application uses Kubernetes Service DNS names:

vote      → vote:5000
redis     → redis:6379
postgres  → postgres:5432
result    → result:4000

External routing is configured through NGINX Ingress:

voting-platform.duckdns.org → vote:5000
result-page.duckdns.org     → result:4000

The Ingress configuration associates both hosts with TLS secrets managed through cert-manager.

Observability

The result service exposes Prometheus metrics at:

/metrics

A Kubernetes ServiceMonitor scrapes the result service every 15 seconds.

The application also defines a PrometheusRule named WorkerDown. It fires when Kubernetes reports fewer than one available replica for the worker Deployment for 1 minute.

The result service additionally publishes a custom gauge:

voting_app_current_votes{option="..."}

which represents the current count for each configured voting option.

CI/CD

The GitHub Actions workflow in .github/workflows/deploy.yaml runs on pushes to main.

flowchart LR
    A[Push to main] --> B[GitHub Actions]
    B --> C[Build vote / worker / result]
    C --> D[Push images to ACR]
    D --> E[Get AKS credentials]
    E --> F[kubectl set image]
    F --> G[Rollout status]

The build job uses a matrix to build the three application images in parallel. Images are tagged with the Git commit SHA and pushed to Azure Container Registry.

The deployment job authenticates to Azure, retrieves AKS credentials, updates the three Kubernetes Deployments, and waits for their rollouts to complete.

This provides a traceable relationship between a Git commit and the container image deployed to AKS.

Deployment note: the checked-in Kubernetes manifests contain versioned image tags, while the GitHub Actions workflow deploys commit-SHA tags with kubectl set image. The CI/CD workflow therefore updates the running Deployment images during deployment.

Configuration

The voting options are centralized through VOTING_OPTIONS:

Python,Java,JavaScript,C++,Go,Rust,C#,C

Local Docker Compose configuration also supplies:

Vote:
  REDIS_HOST=redis

Result:
  DB_HOST=postgres
  DB_USER=postgres
  DB_PASSWORD=postgres
  DB_NAME=postgres

Kubernetes uses the voting-config ConfigMap for the voting options and a Kubernetes Secret for PostgreSQL credentials.

Do not commit real production credentials to source control.

API / Service Endpoints

Service

Endpoint

Purpose

Vote

GET /

Render voting page

Vote

POST /

Queue submitted vote

Result

GET /options

Return configured voting options

Result

GET /metrics

Expose Prometheus metrics

The result application also serves its static frontend and establishes Socket.IO connections for live score updates.

Data Model

The worker creates the following PostgreSQL table when the database is initialized:

CREATE TABLE IF NOT EXISTS votes (
    id VARCHAR(255) NOT NULL UNIQUE,
    vote VARCHAR(255) NOT NULL
);

The worker uses id as the voter identifier. The result service aggregates the vote column using SQL GROUP BY and returns counts for every configured option, including options with zero votes.

Engineering Highlights

Asynchronous persistence

The Flask service does not perform the PostgreSQL write itself. Redis buffers the vote and a separate worker handles persistence.

Producer-consumer architecture

The vote service produces Redis messages while the worker consumes them, creating a clear boundary between request handling and background processing.

Kubernetes-native service discovery

Application components use Kubernetes Service names rather than pod IP addresses, allowing pods to be replaced without changing connection configuration.

Stateful vs stateless workloads

PostgreSQL is represented as a StatefulSet with persistent storage, while the application services and Redis are represented as Deployments.

Health-aware application deployment

The vote and result Deployments define readiness and liveness probes. The result Deployment also defines CPU and memory requests/limits.

Real-time presentation

The result service combines PostgreSQL aggregation with Socket.IO to push updated scores to connected browsers.

Observable deployment

Prometheus-compatible metrics, a ServiceMonitor, and a worker availability alert provide the foundation for Kubernetes-level observability.

Commit-based releases

CI/CD uses the Git SHA as the container image tag, making deployed application versions directly traceable to source revisions.

Production Hardening Opportunities

The current repository is a strong demonstration of containerized and Kubernetes-based architecture, but several areas could be hardened for production:

Add automated unit/integration tests and run them before image publication.

Replace development/default database credentials with managed secret storage.

Disable Flask debug mode in deployed environments.

Add stronger Redis delivery semantics so a worker failure after LPOP cannot lose a queued vote.

Handle only the expected PostgreSQL uniqueness conflict when falling back from INSERT to UPDATE.

Consider Redis persistence/HA if queued votes must survive Redis failure.

Add PostgreSQL backup/restore and HA strategy where required.

Add resource requests/limits and autoscaling policies to additional workloads.

Add richer metrics for queue depth, processing latency, and worker failures.

Harden containers with non-root users and Kubernetes security contexts.

These are improvements, not claims about functionality currently implemented in the repository.

Interview Talking Points

Why Redis?
It provides a lightweight buffer between synchronous vote submission and asynchronous persistence.

Why a separate worker?
It decouples request handling from database processing and demonstrates a producer-consumer pattern.

Why PostgreSQL StatefulSet?
PostgreSQL is stateful and requires persistent storage; the manifest uses a StatefulSet with a PVC.

How are services discovered in Kubernetes?
Kubernetes Services provide stable DNS names such as redis, postgres, vote, and result.

How are live results implemented?
The Node.js service queries PostgreSQL, maintains a per-option Prometheus gauge, and broadcasts scores through Socket.IO every second.

How does CI/CD deploy a new version?
GitHub Actions builds three images in parallel, pushes SHA-tagged images to ACR, obtains AKS credentials, updates the Deployments, and waits for rollouts.

How is PostgreSQL data persisted?
The Kubernetes PostgreSQL StatefulSet uses a volume claim template requesting 1 GiB and mounts it at PostgreSQL's data directory.

How is monitoring implemented?
The result service exposes /metrics; a ServiceMonitor scrapes it every 15 seconds, and a PrometheusRule detects an unavailable worker Deployment.

What is an important reliability trade-off?
The worker removes a vote from Redis before saving it to PostgreSQL. A worker failure between those operations could therefore lose that queued item; reliable acknowledgement/retry semantics would improve the design.

What would you improve for production?
Testing, secret management, queue durability, database HA/backups, resource policies, security hardening, and richer observability would be natural next steps.

License

No license is specified in the repository.
