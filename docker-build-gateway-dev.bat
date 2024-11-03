git pull
docker build -f HLS.Paygate.Gw.Hosting/Dockerfile -t hls2020/nt:gateway_uat .
docker push hls2020/nt:gateway_uat
pause