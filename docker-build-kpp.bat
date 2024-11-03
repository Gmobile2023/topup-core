git pull
docker build -f HLS.Paygate.Kpp.Hosting/Dockerfile -t hls2020/nt:kpp_report .
docker push hls2020/nt:kpp_report

pause