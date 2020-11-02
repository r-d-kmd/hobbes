#!/bin/bash
if [[ ! -z $FEED_PAT ]]
then
    export FEED_USER=$FEED_PAT
    export FEED_PASSWORD=$FEED_PAT
fi