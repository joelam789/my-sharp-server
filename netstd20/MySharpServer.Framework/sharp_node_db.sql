
CREATE SCHEMA `db_sharp_node` DEFAULT CHARACTER SET utf8 ;

CREATE TABLE `db_sharp_node`.`tbl_server_info` (
  `server_name` varchar(32) NOT NULL,
  `group_name` varchar(32) NOT NULL,
  `server_url` varchar(64) NOT NULL COMMENT 'internal access entry',
  `public_url` varchar(64) DEFAULT NULL COMMENT 'public access entry',
  `public_protocol` int(11) NOT NULL DEFAULT '0' COMMENT '0: none, 1: http, 2: ws, 3: https, 4: wss',
  `client_count` int(11) NOT NULL DEFAULT '0' COMMENT 'total number of ws or wss connections',
  `visibility` int(11) NOT NULL DEFAULT '1',
  `public_visibility` int(11) NOT NULL DEFAULT '1',
  `service_list` varchar(1024) NOT NULL,
  `access_key` varchar(64) NOT NULL,
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`server_name`)
) ENGINE=ndbcluster DEFAULT CHARSET=utf8;

