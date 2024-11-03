git pull
docker build -f HLS.Paygate.Report.Hosting/Dockerfile -t hls2020/nt:report .
docker push hls2020/nt:report

pause