git pull
docker build --no-cache -f HLS.Paygate.Balance.Hosting/Dockerfile -t hls2020/nt:balance_uat .
docker push hls2020/nt:balance_uat

pause