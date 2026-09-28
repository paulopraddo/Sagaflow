-- One logical database per service: no service can read another's tables.
CREATE DATABASE catalog;
CREATE DATABASE orders;
CREATE DATABASE inventory;
CREATE DATABASE payments;
