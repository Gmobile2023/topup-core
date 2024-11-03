git pull
docker build -f HLS.Paygate.TopupGw.Hosting/Dockerfile -t hls2020/nt:topup_gate_uat .
docker push hls2020/nt:topup_gate_uat

pause