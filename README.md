pip install -r requirements.txt
Recreate with just this command
app.py is the backend of my website 
index.html is its frontend


Why do we need dockerfile:
Right now, starting this whole app takes you five manual steps in five terminals, in a specific order, and you have to remember all of it yourself: start Redis, start Postgres, dotnet run the worker, node server.js the result app, activate a venv and run python app.py for vote. That's fragile and it's not how this will run in Kubernetes — Kubernetes doesn't know what a venv is, doesn't know what dotnet run means. It only knows one thing: run this container image.


so we basically create our images and then we push those images into ACR through docker then the AKS pulls those images from acr. 
Your laptop
    │
    │ docker push
    ▼
Azure Container Registry (ACR)
    │
    │ docker pull
    ▼
AKS cluster

always mention the version of the image otherwise kubernetes cant know which is old and which is new version while updating the image 


the diffeerence between the deployment and statefulSet is that deployment does not have a persistent storage for its pods....lets assume that the postgres pod dies then the new pods needs the old data so we use statefulSet instead of deployment


An Ingress resource — a YAML file, just a set of rules ("send vote.local traffic to the vote Service"). On its own, this does nothing. It's a description, not a mechanism.
An Ingress Controller — actual running pods whose job is to read those rules and actually enforce them — this is the real traffic router.