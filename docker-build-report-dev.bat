git pull
docker build -f HLS.Paygate.Report.Hosting/Dockerfile -t hls2020/nt:report_uat .
docker push hls2020/nt:report_uat

pause