# NeoBlockAnalysis
[简体中文](#zh) |    [English](#en) 

<a name="zh">简体中文</a>
## 概述 :
_[NeoBlock-Mongo-Storage](https://github.com/NewEconoLab/NeoBlock-Mongo-Storage)_ 项目爬取入库了一些基础的区块链数据。
但是并没有满足所有的需求，例如资产排名和历史交易记录分析等。所以这个工程的目的就是对基础数据进一步加工，得到一些定制化的数据结构。
**需要注意的是此工程的并不是直接从节点获取数据，而是对已有的数据进行进一步的加工。**


## 部署演示 :

安装git（如果已经安装则跳过） :
```
yum install git -y
```

安装 dotnet sdk :
```
rpm -Uvh https://packages.microsoft.com/config/rhel/7/packages-microsoft-prod.rpm
yum update
yum install libunwind libicu -y
yum install dotnet-sdk-2.1.200 -y
```

通过git将本工程下载到服务器 :
```
git clone https://github.com/NewEconoLab/NeoBlockAnalysis.git
```

修改配置文件放在执行文件下，配置文件大致如下 :
```json
{
  "mongodbConnStr_testnet": "测试网分析入库数据库链接",
  "mongodbDatabase_testnet": "测试网分析入库数据库名称",
  "mongodbConnStr_mainnet": "主网分析入库数据库链接",
  "mongodbDatabase_mainnet": "主网分析入库数据库名称",
  "neo_mongodbConnStr_testnet":"测试网基础数据库（mongo-storage工程入的数据）链接",
  "neo_mongodbDatabase_testnet": "测试网基础数据库名称",
  "neo_mongodbConnStr_mainnet": "主网基础数据库链接",
  "neo_mongodbDatabase_mainnet": "主网基础数据库名称",
  "DoDumpInfo":"0",
  "sleepTime":"200",
  "MongoDbIndexs": [
  ]
}
```
```
>MongoDbIndexs里是数据库的索引，格式类似:"{\"collName\": \"address_tx\",\"indexs\": [{\"indexName\": \"i_blockindex\",\"indexDefinition\": {\"blockindex\": 1,},\"isUnique\": false}]}"
```


编译并运行
```
dotnet publish
cd  NeoBlockAnalysis/NeoBlockAnalysis/bin/Debug/netcoreapp2.0
dotnet NeoBlockAnalysis.dll
```


<a name="en">English</a>
## Overview :
_[NeoBlock-Mongo-Storage](https://github.com/NewEconoLab/NeoBlock-Mongo-Storage)_ The project crawled into some basic blockchain data. But it does not meet all the needs, such as asset rankings and historical transaction analysis. So the purpose of this project is to further process the underlying data and get some customized data structures. ** It should be noted that this project does not directly acquire data from the node, but further processes the existing data. **

## Deployment

install git（Skip if already installed） :
```
yum install git -y
```

install dotnet sdk :
```
rpm -Uvh https://packages.microsoft.com/config/rhel/7/packages-microsoft-prod.rpm
yum update
yum install libunwind libicu -y
yum install dotnet-sdk-2.1.200 -y
```

clone to the server :
```
git clone https://github.com/NewEconoLab/NeoBlockAnalysis.git
```

Modify the configuration file under the execution file, the configuration file is roughly as follows:
```json
{
  "mongodbConnStr_testnet": "analysis database connectString at testnet",
  "mongodbDatabase_testnet": "analysis database name at testnet",
  "mongodbConnStr_mainnet": "analysis database connectString at mainnet",
  "mongodbDatabase_mainnet": "analysis database name at mainnet",
  "neo_mongodbConnStr_testnet":"basic database connectString at testnet",
  "neo_mongodbDatabase_testnet": "basic database name at testnet",
  "neo_mongodbConnStr_mainnet": "basic database connectString at mainnet",
  "neo_mongodbDatabase_mainnet": "basic database name at mainnet",
  "DoDumpInfo":"0",
  "sleepTime":"200",
  "MongoDbIndexs": [
  ]
}
```
```
>MongoDbIndexs is the index of the database, the format is similar:"{\"collName\": \"address_tx\",\"indexs\": [{\"indexName\": \"i_blockindex\",\"indexDefinition\": {\"blockindex\": 1,},\"isUnique\": false}]}"
```

Compile and run :
```
dotnet publish
cd  NeoBlockAnalysis/NeoBlockAnalysis/bin/Debug/netcoreapp2.0
dotnet NeoBlockAnalysis.dll
```
