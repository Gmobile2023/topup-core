git pull
docker build -f HLS.Paygate.Backend.Hosting/Dockerfile -t hls2020/nt:backend_uat .
docker push hls2020/nt:backend_uat

pause