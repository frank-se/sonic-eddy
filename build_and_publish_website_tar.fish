#!/bin/fish
cd website
zola build
tar -czf site.tar.gz -C public .
mv site.tar.gz ..
cd ..
hut pages publish -d sonic-eddy.org site.tar.gz
