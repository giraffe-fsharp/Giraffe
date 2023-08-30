#!/bin/bash

set -euo pipefail

# ==============================================
# Warning:
#
# Make sure that the server is already running in a different terminal.

function test_not_cached {
    for counter in {1..5}
    do
        curl "localhost:5000/cached/not"
        echo
        sleep 1
    done
}

function test_public_cached {
    for counter in {1..5}
    do
        curl "localhost:5000/cached/public"
        echo
        sleep 1
    done
}

function test_private_cached {
    for counter in {1..5}
    do
        curl "localhost:5000/cached/private"
        echo
        sleep 1
    done
}

function test_public_cached_no_vary_by_query_keys {
    curl "localhost:5000/cached/vary/not?query1=a&query2=b"
    echo
    sleep 1
    curl "localhost:5000/cached/vary/not?query1=a&query2=b"
    echo
    sleep 1
    curl "localhost:5000/cached/vary/not?query1=c&query2=d"
    echo
    sleep 1
    curl "localhost:5000/cached/vary/not?query1=c&query2=d"
    echo
    sleep 1
}

function test_cached_vary_by_query_keys {
    curl "localhost:5000/cached/vary/yes?query1=a&query2=b"
    echo
    sleep 1
    curl "localhost:5000/cached/vary/yes?query1=a&query2=b"
    echo
    sleep 1
    curl "localhost:5000/cached/vary/yes?query1=c&query2=d"
    echo
    sleep 1
    curl "localhost:5000/cached/vary/yes?query1=c&query2=d"
    echo
    sleep 1
}

echo "-----------------------------------"
echo "Testing the /cached/not endpoint"
echo
time test_not_cached


echo "-----------------------------------"
echo "Testing the /cached/public endpoint"
echo
time test_public_cached

echo "-----------------------------------"
echo "Testing the /cached/private endpoint"
echo
time test_private_cached

echo "-----------------------------------"
echo "Testing the /cached/vary/not endpoint"
echo
time test_public_cached_no_vary_by_query_keys

echo "-----------------------------------"
echo "Testing the /cached/vary/yes endpoint"
echo
time test_cached_vary_by_query_keys