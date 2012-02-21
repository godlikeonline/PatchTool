#!/usr/bin/perl
# http://stackoverflow.com/questions/224687/git-find-duplicate-blobs-files-in-this-tree

# usage: git ls-tree -r HEAD | $PROGRAM_NAME

use strict;
use warnings;

my $sha1_path = {};

while (my $line = <STDIN>) {
    chomp $line;

    if ($line =~ m{ \A \d+ \s+ \w+ \s+ (\w+) \s+ (\S+) \z }xms) {
        my $sha1 = $1;
        my $path = $2;

        push @{$sha1_path->{$sha1}}, $path;
    }
}

foreach my $sha1 (keys %$sha1_path) {
    if (scalar @{$sha1_path->{$sha1}} > 1) {
        foreach my $path (@{$sha1_path->{$sha1}}) {
            print "$sha1  $path\n";
        }

        print '-' x 40, "\n";
    }
}

