openssl genrsa -out private.key 2048
openssl req -new -x509 -key private.key -out certificate.crt -days 36500 -config "openssl.cnf"
openssl pkcs12 -export -out cert.pfx -inkey private.key -in certificate.crt