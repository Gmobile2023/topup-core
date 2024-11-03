git pull
docker build -f HLS.Paygate.Worker.Hosting/Dockerfile -t hls2020/nt:worker_uat .
docker push hls2020/nt:worker_uat
pause