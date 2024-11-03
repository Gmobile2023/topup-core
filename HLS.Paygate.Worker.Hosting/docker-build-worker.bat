docker build -t nt-worker .
docker tag nt-worker nhannv/nt:worker_dev
docker push nhannv/nt:worker_dev

pause