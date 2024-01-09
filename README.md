# QQChatRecordArchiveConverter

将QQ导出的MHT聊天记录处理后写入数据库，图片保存为文件。数据库中的聊天记录可直接查看，也可导出为CSV、HTML等格式。主要是为了将群内不同用户导出的聊天记录全部整合。

---

## 用法

### 导入

启动程序，点击导入，选中MHT文件。

### 导出

点击导出，选择文件保存的位置。
注意：

- 导出的HTML文件使用相对路径引用图片资源，因此HTML文件所在的文件夹下需要包含`Sources`文件夹以及内含图片，否则HTML中的图片将无法显示。
- 只有搜索结果会被导出，因此首先需要在窗口左侧使用合适的条件搜索。

## 功能

### 冗余压缩

QQ导出的MHT文件Style为在Element设定，故文件中有大量重复的style，程序在提取时将只提取文本及图像内容，抛弃style。

### 图片分离

MHT中的图片将会被提取并以SHA1命名，最大程度上去除重复图片，且方便查看图片。

### 发送者处理

MHT中发送者类似`昵称(id)`或`昵称<id>`，将会被分解为`SenderName`和`SenderId`，方便后续处理以及查找。

### 防重复

因QQ导出的MHT文件不含任何有关消息的唯一标识符，所以为了防止导入群聊内不同用户提供的MHT导致的重复，`Message`以`Content`, `SenderId`和`SendTimeMinute`设定Unique CompositeKey，即当前分钟内，同一发送者发送的内容相同的消息会被判定为重复。设定为分钟是因为不同用户导出的MHT中收到消息的计时不一定一致。
因为不同用户导出的MHT中同一条消息的格式不一定一致，所以同一条消息的原文`OriginMessage`不一定相同，故只能采用显示内容`Content`来防止重复。`Content`会区别不同的图像`<img>`，因为图像名称为SHA1。
又因为QQ导出的MHT中发送者ID不一定一致（有出现有些人的聊天记录里是邮箱，有些人的聊天记录里是QQ号的情况），所以该策略无法防止这种情况造成的重复。
还因为QQ导出的MHT中部分不能导出的消息（合并聊天记录、语音、卡片消息、视频、群文件等）会被导出为空白记录，不包含任何信息，`Content`会将其显示为`[消息类型不支持导出，该记录无任何数据]`。意味着当前分钟内，同一发送者发送的多条不同的该类消息会被判定为重复（不过无所谓，反正都是空白的，留一堆空白也没有意义）。
起因都是因为QQ导出的MHT中不带任何消息id。

### 导出HTML

没啥好说的，由于图片分离，并且使用先进的class而不是直接在Element上设定样式，体积大大减小，并且重写了个css，比MHT不知道要好看到哪里去了。发给别人的时候注意带图片。

### 搜索

搜索想要的消息，并在右侧窗口查看。由于本人过于菜了，不会图文混排，所以现在带图消息只能看着`img`标签想象他是个啥。

### 备份

顾名思义

### Fix

没事别动

### Vac

Sqlite Vacuum
