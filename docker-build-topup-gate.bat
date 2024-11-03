git pull
docker build -f HLS.Paygate.TopupGw.Hosting/Dockerfile -t hls2020/nt:topup_gate .
docker push hls2020/nt:topup_gate

pause