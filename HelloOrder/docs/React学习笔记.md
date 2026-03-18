# React 常用知识与本项目应用（详细版）

本文档分为三部分：**React 常用知识**（含大量示例）、**本项目中用到的知识**（路由、状态、UI、请求），以及**注意事项**（易错点与正确写法对比）。每个概念都配有可运行的示例或本项目中的真实代码片段。

---

## 一、React 常用知识

### 1.1 组件与 JSX

#### 什么是组件

组件就是**返回一段 UI 的函数**（或类，本项目只用函数）。一个页面可以由多个小组件拼成。

**示例：最简单的组件**

```tsx
// 无参数，只返回静态内容
function Welcome() {
  return <h1>欢迎使用 HelloOrder</h1>;
}

// 使用：像 HTML 标签一样
<Welcome />
```

**示例：本项目中的真实组件（Login 页面最简版）**

```tsx
export default function Login() {
  return (
    <div style={{ height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
      <Card title="HelloOrder 外贸业务管理系统" style={{ width: 400 }}>
        <Form onFinish={onFinish}>
          <Form.Item name="username" rules={[{ required: true, message: '请输入用户名' }]}>
            <Input placeholder="用户名" />
          </Form.Item>
          <Button type="primary" htmlType="submit">登录</Button>
        </Form>
      </Card>
    </div>
  );
}
```

#### JSX 必须遵守的规则

| 规则 | 错误示例 | 正确示例 |
|------|-----------|----------|
| 标签必须闭合 | `<Input>` | `<Input />` 或 `<Input></Input>` |
| 多个顶层要包一层 | `return (<h1></h1><p></p>);` | `return (<div><h1></h1><p></p></div>);` 或 `return (<><h1></h1><p></p></>);` |
| 用 className 不用 class | `<div class="box">` | `<div className="box">` |
| 插入 JS 用花括号 | `<span>user.name</span>`（会当字符串） | `<span>{user?.realName}</span>` |

**示例：在属性里用表达式**

```tsx
// 本项目 Layout 中：根据当前路径高亮菜单
<Menu selectedKeys={[location?.pathname ?? '/']} />

// 等价于：若 location.pathname 是 '/merchants'，则 selectedKeys={['/merchants']}
```

**示例：条件渲染（在 JSX 里）**

```tsx
// 只有 token 存在时才显示子内容；否则显示重定向
if (!token) return <Navigate to="/login" replace />;
return <>{children}</>;

// 另一种：在 JSX 里写条件
<span>{user?.realName || user?.username}</span>  // 有 realName 显示 realName，否则显示 username
```

---

### 1.2 Props（属性）

父组件通过**标签上的属性**把数据传给子组件，子组件用**第一个参数**接收（通常解构）。

#### 示例：只传 children

```tsx
// 父组件（App.tsx）
<PrivateRoute>
  <Layout />
</PrivateRoute>

// 子组件：children 就是 <Layout />
function PrivateRoute({ children }: { children: React.ReactNode }) {
  const { token } = useAuth();
  if (!token) return <Navigate to="/login" replace />;
  return <>{children}</>;   // 相当于渲染 <Layout />
}
```

#### 示例：传多个属性（假设我们写一个可复用的卡片）

```tsx
interface CardProps {
  title: string;
  width?: number;
  children: React.ReactNode;
}

function MyCard({ title, width = 400, children }: CardProps) {
  return (
    <div style={{ width, border: '1px solid #eee', padding: 16 }}>
      <h3>{title}</h3>
      {children}
    </div>
  );
}

// 使用
<MyCard title="新增商家" width={500}>
  <Form>...</Form>
</MyCard>
```

#### 示例：本项目 Modal 的 open / onCancel

```tsx
<Modal
  title="新增商家"
  open={modalOpen}                    // 布尔值：是否显示
  onCancel={() => setModalOpen(false)} // 点遮罩或取消时关闭
  footer={null}                       // 不显示默认底部按钮
>
  <Form onFinish={onAdd}>...</Form>
</Modal>
```

---

### 1.3 State（状态）与 useState

状态是**会随着用户操作或请求结果而变化**的数据；状态一变，组件会**重新渲染**，界面随之更新。

#### 基本用法

```tsx
const [当前值, 更新函数] = useState(初始值);

// 例如
const [page, setPage] = useState(1);           // 数字
const [loading, setLoading] = useState(false);  // 布尔
const [name, setName] = useState('');           // 字符串
const [list, setList] = useState<Merchant[]>([]); // 数组，泛型指定元素类型
```

#### 正确更新 vs 错误更新

```tsx
// ✅ 正确：用 set 函数
setPage(2);
setLoading(true);
setName('张三');
setList([...list, newItem]);  // 数组要新引用，不能 list.push(newItem) 后 setList(list)

// ❌ 错误：直接改变量不会触发重新渲染
page = 2;        // 无效
list.push(item); // 无效，且违反“不可变数据”习惯
```

#### 本项目中的完整例子：商家列表的状态

```tsx
export default function MerchantList() {
  const [list, setList] = useState<Merchant[]>([]);   // 表格数据
  const [total, setTotal] = useState(0);               // 总条数（分页用）
  const [page, setPage] = useState(1);                 // 当前页
  const [pageSize, setPageSize] = useState(20);        // 每页条数
  const [loading, setLoading] = useState(false);       // 是否在请求中（表格 loading）
  const [name, setName] = useState('');               // 筛选：商家名称
  const [modalOpen, setModalOpen] = useState(false);   // 新增弹窗是否打开

  const load = async () => {
    setLoading(true);
    try {
      const res = await api.get(`/merchants?page=${page}&pageSize=${pageSize}&name=${encodeURIComponent(name)}`);
      if (res.code === 0 && res.data) {
        setList(res.data.list);   // 更新列表 → 表格重绘
        setTotal(res.data.total); // 更新总数 → 分页器更新
      }
    } finally {
      setLoading(false);
    }
  };

  // 用户点“查询”时：先回到第 1 页，再请求
  const onSearch = () => {
    setPage(1);
    load();
  };

  // 用户点“新增商家”时：打开弹窗
  const openModal = () => setModalOpen(true);
}
```

---

### 1.4 副作用与 useEffect

**副作用**：和“渲染结果”没有直接关系的操作，例如：发请求、订阅、改 document.title、操作 DOM。这些要放在 **useEffect** 里，在“渲染完成之后”执行。

#### 三种依赖写法

```tsx
// 1）不写依赖数组：每次组件渲染后都执行（极少用，容易死循环）
useEffect(() => {
  console.log('每次渲染都跑');
});

// 2）空数组 []：只在组件“挂载”时执行一次（例如只请求一次配置）
useEffect(() => {
  fetchConfig();
}, []);

// 3）有依赖 [page, pageSize]：挂载时执行一次，且 page 或 pageSize 变化时再执行（本项目列表页常用）
useEffect(() => {
  load();
}, [page, pageSize]);
```

#### 本项目列表页的完整流程示例

```tsx
// 用户打开“商家管理”页面
// → 组件挂载，useEffect 执行，load() 被调用，请求第 1 页数据
// → 用户点击分页“第 2 页”
// → setPage(2) 触发重新渲染
// → useEffect 的依赖 [page, pageSize] 中 page 变了，effect 再次执行，load() 请求第 2 页

useEffect(() => {
  load();
}, [page, pageSize]);
```

#### 为什么不要把 load 放进依赖？

```tsx
// ❌ 危险写法
useEffect(() => {
  load();
}, [load]);  // load 是函数，每次渲染都是新的引用 → effect 每次都跑 → load 里又 setState → 又渲染 → 无限循环

// ✅ 本项目写法：依赖只写“真正会变且需要重新请求”的量
useEffect(() => {
  load();
}, [page, pageSize]);
// load 内部会读到当前渲染时的 page、pageSize，所以不需要把 load 放进依赖
```

---

### 1.5 useCallback

**问题**：每次渲染时，函数会重新创建，引用会变。如果把这个函数作为 props 传给子组件，子组件可能因此频繁重渲染。

**useCallback(fn, [依赖])**：依赖不变时，返回**同一个函数引用**。

#### 本项目 AuthContext 中的例子

```tsx
const login = useCallback(async (username: string, password: string) => {
  const res = await fetch('/api/auth/login', { ... });
  const json = await res.json();
  if (json.code !== 0) return { ok: false, message: json.message };
  setToken(d.token);
  setUser({ userId: d.userId, username: d.username, ... });
  return { ok: true };
}, [setUser]);  // setUser 本身也是 useCallback 出来的，引用稳定

// 这样 AuthContext.Provider 的 value 里 login 引用稳定，消费 Context 的组件不会因为“login 变了”而重渲染
```

**对比：不用 useCallback 时**

```tsx
// 每次 AuthProvider 渲染，login 都是新函数
const login = async (username: string, password: string) => { ... };
// Provider value={{ token, user, login, logout }}
// 子组件 useAuth() 拿到的 login 每次都在变 → 可能触发不必要的重渲染
```

---

### 1.6 Context 与 useContext

**Context**：在组件树中“跨层”传数据，不用一层层 props。

#### 创建 → 提供 → 消费

```tsx
// 1）创建：并约定“值”的类型
interface AuthContextType {
  token: string | null;
  user: UserInfo | null;
  login: (username: string, password: string) => Promise<{ ok: boolean; message?: string }>;
  logout: () => void;
}
const AuthContext = createContext<AuthContextType | null>(null);

// 2）提供：在顶层用 Provider 包住整棵子树，value 就是“要传下去的数据”
export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [token, setToken] = useState<string | null>(() => localStorage.getItem(TOKEN_KEY));
  const [user, setUserState] = useState<UserInfo | null>(...);
  const login = useCallback(async (...) => { ... }, [setUser]);
  const logout = useCallback(() => { ... }, [setUser]);
  return (
    <AuthContext.Provider value={{ token, user, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

// 3）消费：任意深层子组件用 useContext 取用
export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
```

#### 在页面里怎么用

```tsx
// Login 页面：需要调用 login
function Login() {
  const { login } = useAuth();
  const onFinish = async (v) => {
    const result = await login(v.username, v.password);
    if (result.ok) message.success('登录成功');
    else message.error(result.message);
  };
  return <Form onFinish={onFinish}>...</Form>;
}

// Layout：需要显示用户名、退出
function Layout() {
  const { user, logout } = useAuth();
  return (
    <span>{user?.realName || user?.username}</span>
    <Button onClick={() => { logout(); navigate('/login'); }}>退出</Button>
  );
}

// PrivateRoute：需要判断 token
function PrivateRoute({ children }) {
  const { token } = useAuth();
  if (!token) return <Navigate to="/login" replace />;
  return <>{children}</>;
}
```

---

### 1.7 事件处理与受控组件

#### 事件：传函数，不要传“函数调用”

```tsx
// ❌ 错误：onClick={handleClick()} 会在渲染时立刻执行 handleClick，而不是点击时执行
<Button onClick={handleClick()}>确定</Button>

// ✅ 正确：传函数引用，点击时再执行
<Button onClick={handleClick}>确定</Button>
<Button onClick={() => setModalOpen(true)}>新增</Button>
```

#### 受控组件：输入框的值由 state 控制

```tsx
const [name, setName] = useState('');

// 输入框的 value 来自 state，变化通过 onChange 写回 state
<Input
  placeholder="商家名称"
  value={name}
  onChange={e => setName(e.target.value)}
  onPressEnter={onSearch}
/>
// 用户每输入一个字符 → onChange → setName → 重新渲染 → 输入框显示新值
```

#### 本项目 Ant Design Form：由 Form 内部“受控”

```tsx
const [form] = Form.useForm();

<Form form={form} onFinish={onAdd}>
  <Form.Item name="name" label="商家名称" rules={[{ required: true }]}>
    <Input />
  </Form.Item>
  <Form.Item name="platform" label="平台">
    <Input />
  </Form.Item>
  <Button type="primary" htmlType="submit">确定</Button>
</Form>

// 用户点“确定”且校验通过后，onFinish 被调用，参数就是所有 name 对应的值：
const onAdd = async (v: Record<string, unknown>) => {
  // v = { name: 'xx', platform: 'Shopee', ... }
  await api.post('/merchants', { name: v.name, platform: v.platform, ... });
};
```

---

### 1.8 条件渲染与列表

#### 条件渲染

```tsx
// 只渲染一种：condition 为 true 才渲染 <Component />
{condition && <Component />}

// 二选一
{condition ? <ComponentA /> : <ComponentB />}

// 本项目：未登录就重定向，否则渲染子路由
if (!token) return <Navigate to="/login" replace />;
return <>{children}</>;
```

#### 列表与 key

```tsx
// 自己 map 时：每个顶层元素必须有 key，且尽量用稳定唯一 id
{list.map(item => (
  <div key={item.id}>
    <span>{item.name}</span>
  </div>
))}

// ❌ 用 index 当 key：列表会增删、排序时容易错乱
{list.map((item, index) => <div key={index}>...</div>)}

// 本项目用 Table：rowKey 就是“每一行的 key”
<Table rowKey="id" dataSource={list} columns={columns} />
```

---

### 1.9 TypeScript 与 React

#### 为组件 props 写类型

```tsx
interface Props {
  title: string;
  onClose?: () => void;
  children?: React.ReactNode;
}

function MyModal({ title, onClose, children }: Props) {
  return (
    <div>
      <h3>{title}</h3>
      {children}
      {onClose && <Button onClick={onClose}>关闭</Button>}
    </div>
  );
}
```

#### 为 state 写类型（泛型）

```tsx
const [list, setList] = useState<Merchant[]>([]);
// setList 只能传 Merchant[] 或 (prev => Merchant[])，否则报错

const [user, setUser] = useState<UserInfo | null>(null);
```

#### 为接口返回写类型（api 泛型）

```tsx
interface Merchant {
  id: string;
  name: string;
  platform?: string;
  createdAt: string;
}

const res = await api.get<{ list: Merchant[]; total: number; page: number; pageSize: number }>(
  `/merchants?page=${page}&pageSize=${pageSize}`
);
// res.data 的类型就是 { list: Merchant[]; total: number; ... }
// 写 res.data.list 时会有自动补全和类型检查
```

#### 可选链与默认值

```tsx
// 避免 user 为 null/undefined 时报错
user?.realName
user?.realName || user?.username
location?.pathname ?? '/'
```

---

## 二、本项目中用到的知识

### 2.1 项目技术栈

| 技术 | 用途 |
|------|------|
| React 18 | UI 库 |
| TypeScript | 类型 |
| Vite 5 | 构建与开发服务器 |
| React Router 6 | 路由 |
| Ant Design 5 | 组件库（Form、Table、Modal、Button 等） |

---

### 2.2 路由（React Router 6）详解

#### 整体结构（本项目 App.tsx）

```tsx
<BrowserRouter>                    {/* 最外层：使用 HTML5 History */}
  <AuthProvider>
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/" element={<PrivateRoute><Layout /></PrivateRoute>}>
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route path="dashboard" element={<Dashboard />} />
        <Route path="merchants" element={<MerchantList />} />
        <Route path="orders" element={<OrderList />} />
        <Route path="system/users" element={<UserList />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  </AuthProvider>
</BrowserRouter>
```

- **path="/"** + **element={<Layout />}**：访问 `/`、`/dashboard`、`/merchants` 等时，都会先渲染外层的 `Layout`。
- **Route index**：当路径**恰好**是父路径 `/` 时，渲染 `<Navigate to="/dashboard" />`，即默认跳到工作台。
- **path="*"**：上面都没匹配到时（例如用户输入了不存在的 URL），重定向到 `/`。

#### Outlet：子路由的“插槽”

```tsx
// Layout.tsx：侧栏 + 顶栏是固定的，中间“内容区”由子路由决定
return (
  <AntLayout>
    <Sider>...</Sider>
    <AntLayout>
      <Header>...</Header>
      <Content>
        <Outlet />   {/* 这里会渲染当前匹配的子路由：Dashboard / MerchantList / OrderList / UserList */}
      </Content>
    </AntLayout>
  </AntLayout>
);
```

- 访问 `/merchants` 时：外层是 Layout，`<Outlet />` 位置渲染 `<MerchantList />`。
- 访问 `/orders` 时：同样是 Layout，`<Outlet />` 位置渲染 `<OrderList />`。

#### 编程式跳转：useNavigate

```tsx
const navigate = useNavigate();

// 登录成功后跳转到工作台
navigate('/dashboard');

// 退出后跳转到登录页
logout();
navigate('/login');

// 带 replace：不往 history 里压新记录，用户点“后退”不会回到登录页
navigate('/dashboard', { replace: true });
```

#### 当前路径：useLocation

```tsx
const location = useLocation();
// location.pathname 例如 '/merchants'、'/orders'

// 本项目：侧栏菜单根据 pathname 高亮当前项
<Menu selectedKeys={[location?.pathname ?? '/']} items={menuItems} onClick={({ key }) => navigate(key)} />
```

---

### 2.3 状态与数据流（结合本项目）

#### 全局状态：AuthContext

- **存什么**：token、user（userId、username、realName、roleCode、permissions）。
- **谁提供**：`AuthProvider` 包在 App 最外层。
- **谁消费**：Login（login）、Layout（user、logout）、PrivateRoute（token）。
- **持久化**：登录成功后写入 `localStorage`；刷新页面时从 localStorage 读入，恢复 token 和 user。

#### 页面内状态：以商家列表为例

```tsx
// 服务端数据
const [list, setList] = useState<Merchant[]>([]);
const [total, setTotal] = useState(0);

// 分页
const [page, setPage] = useState(1);
const [pageSize, setPageSize] = useState(20);

// 请求中
const [loading, setLoading] = useState(false);

// 筛选
const [name, setName] = useState('');

// 弹窗
const [modalOpen, setModalOpen] = useState(false);
```

数据流简述：

1. 用户打开页面 → `useEffect` 调 `load()` → `api.get` 请求 → `setList` / `setTotal` → 表格更新。
2. 用户改分页 → `Table` 的 `onChange` 里 `setPage` / `setPageSize` → 依赖 `[page, pageSize]` 的 `useEffect` 再跑 → 再次 `load()` → 表格更新。
3. 用户点“查询” → `onSearch` 里 `setPage(1)` 再 `load()` → 从第一页按当前 name 查。

---

### 2.4 Ant Design 在本项目中的用法（带示例）

#### 全局中文：main.tsx

```tsx
import zhCN from 'antd/locale/zh_CN';
import { ConfigProvider } from 'antd';

ReactDOM.createRoot(document.getElementById('root')!).render(
  <ConfigProvider locale={zhCN}>
    <App />
  </ConfigProvider>
);
```

#### Form：登录表单示例

```tsx
<Form name="login" onFinish={onFinish} autoComplete="off" size="large">
  <Form.Item name="username" rules={[{ required: true, message: '请输入用户名' }]}>
    <Input prefix={<UserOutlined />} placeholder="用户名" />
  </Form.Item>
  <Form.Item name="password" rules={[{ required: true, message: '请输入密码' }]}>
    <Input.Password prefix={<LockOutlined />} placeholder="密码" />
  </Form.Item>
  <Form.Item>
    <Button type="primary" htmlType="submit" block loading={loading}>
      登录
    </Button>
  </Form.Item>
</Form>
```

- **name**：字段名，`onFinish` 收到的对象里就用这个 key。
- **rules**：校验规则，不通过不会调 `onFinish`。
- **htmlType="submit"**：点击后触发表单提交（触发 `onFinish`），`type="primary"` 只是按钮样式。

#### Table：列表 + 分页示例

```tsx
<Table
  rowKey="id"
  loading={loading}
  dataSource={list}
  columns={[
    { title: '商家名称', dataIndex: 'name', key: 'name' },
    { title: '平台', dataIndex: 'platform', key: 'platform' },
    {
      title: '创建时间',
      dataIndex: 'createdAt',
      key: 'createdAt',
      render: (t: string) => t ? new Date(t).toLocaleString() : '-'
    }
  ]}
  pagination={{
    current: page,
    pageSize,
    total,
    showSizeChanger: true,
    showTotal: (t) => `共 ${t} 条`
  }}
  onChange={p => {
    setPage(p.current || 1);
    setPageSize(p.pageSize || 20);
  }}
/>
```

- **rowKey="id"**：每行用 `id` 当 key，必填。
- **dataIndex**：对应后端返回的字段名（如 `name`、`createdAt`）。
- **render**：自定义单元格内容，如日期格式化、状态码转文案。
- **onChange**：分页、排序、筛选变化时触发，参数里有 `current`、`pageSize` 等。

#### Modal + Form：新增商家弹窗

```tsx
<Modal
  title="新增商家"
  open={modalOpen}
  onCancel={() => setModalOpen(false)}
  footer={null}
>
  <Form form={form} onFinish={onAdd} layout="vertical">
    <Form.Item name="name" label="商家名称" rules={[{ required: true }]}>
      <Input />
    </Form.Item>
    {/* ... */}
    <Form.Item>
      <Space>
        <Button type="primary" htmlType="submit">确定</Button>
        <Button onClick={() => setModalOpen(false)}>取消</Button>
      </Space>
    </Form.Item>
  </Form>
</Modal>

// 提交成功后：关弹窗、清空表单、刷新列表
const onAdd = async (v) => {
  const res = await api.post('/merchants', v);
  if (res.code === 0) {
    message.success('添加成功');
    setModalOpen(false);
    form.resetFields();
    load();
  }
};
```

#### message 轻提示

```tsx
import { message } from 'antd';

message.success('登录成功');
message.error(res.message || '添加失败');
```

---

### 2.5 请求封装（services/api.ts）示例

#### 如何带 Token 与处理 401

```tsx
async function request<T>(path: string, options: RequestInit = {}): Promise<...> {
  const token = getStoredToken();  // 每次请求都从 localStorage 取最新 token
  const headers = { 'Content-Type': 'application/json', ...options.headers };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${API_BASE}${path}`, { ...options, headers });
  const json = await res.json();

  if (res.status === 401) {
    localStorage.removeItem('helloorder_token');
    localStorage.removeItem('helloorder_user');
    window.location.href = '/login';
  }
  return json;
}
```

#### 在页面里怎么用

```tsx
// GET
const res = await api.get<{ list: Merchant[]; total: number }>(
  `/merchants?page=${page}&pageSize=${pageSize}&name=${encodeURIComponent(name)}`
);

// POST
const res = await api.post<unknown>('/merchants', {
  name: v.name,
  platform: v.platform,
  shopName: v.shopName,
  contact: v.contact,
  settlementType: v.settlementType
});

if (res.code === 0 && res.data) {
  setList(res.data.list);
  setTotal(res.data.total);
} else {
  message.error(res.message || '操作失败');
}
```

---

### 2.6 项目目录与职责（简要）

| 目录/文件 | 职责 |
|-----------|------|
| **src/main.tsx** | 入口：createRoot、ConfigProvider、挂载 App |
| **src/App.tsx** | BrowserRouter、AuthProvider、Routes、PrivateRoute |
| **src/layout/Layout.tsx** | 侧栏菜单、顶栏用户、`<Outlet />` |
| **src/pages/Login.tsx** | 登录表单、调用 useAuth().login |
| **src/pages/Dashboard.tsx** | 工作台占位 |
| **src/pages/merchant/MerchantList.tsx** | 商家列表、分页、筛选、新增弹窗 |
| **src/pages/order/OrderList.tsx** | 订单列表、分页、状态映射 |
| **src/pages/system/UserList.tsx** | 用户列表（Admin） |
| **src/store/AuthContext.tsx** | 登录态 Context、useAuth、login/logout |
| **src/services/api.ts** | request、api.get/post、401 处理 |

---

## 三、注意事项（含错误 vs 正确示例）

### 3.1 useEffect 依赖与无限循环

**错误示例：把 load 放进依赖**

```tsx
const load = async () => {
  setLoading(true);
  const res = await api.get(`/merchants?page=${page}&pageSize=${pageSize}`);
  setList(res.data.list);
  setLoading(false);
};
useEffect(() => {
  load();
}, [load]);  // load 每次渲染都是新函数 → effect 每次渲染都执行 → setList 又触发渲染 → 死循环
```

**正确示例：依赖只写会变化且需要重新请求的量**

```tsx
useEffect(() => {
  load();
}, [page, pageSize]);
// load 内部用到的 page、pageSize 是当前渲染闭包里的值，依赖变化时 effect 会重新跑，能拿到最新 page/pageSize
```

若 ESLint 提示“缺少 load”，在确认不会死循环的前提下，可对该行加：

```tsx
// eslint-disable-next-line react-hooks/exhaustive-deps
```

---

### 3.2 路由守卫与 token

- 需登录的页面都要放在 **PrivateRoute** 里，否则未登录用户可直接访问 `/merchants` 等 URL。
- 请求头里的 token 要用 **getStoredToken()** 从 localStorage 取，不要用 Context 里存的 token（避免闭包拿到旧 token，例如刚登录完第一次请求可能还没拿到新 value）。

---

### 3.3 Table：rowKey、dataIndex、render

**rowKey 必填且唯一**

```tsx
<Table rowKey="id" dataSource={list} ... />
// 若没有 id，可用其他唯一字段，如 rowKey="orderNo"
```

**dataIndex 与后端字段一致**

- 后端若返回 `created_at`，要么后端转成 camelCase 的 `createdAt`，要么 columns 里写 `dataIndex: 'created_at'`。

**render 格式化**

```tsx
{
  title: '状态',
  dataIndex: 'status',
  key: 'status',
  render: (s: number) => statusMap[s] ?? s
}
{
  title: '金额',
  dataIndex: 'totalAmount',
  key: 'totalAmount',
  render: (v: number, r: OrderRow) => `${r.currency} ${v}`
}
```

---

### 3.4 表单：Ant Design Form

- Form 已经“受控”了字段，**不要**再用 `useState` 存同名字段，否则容易冲突。
- **Modal 关闭时**建议 `form.resetFields()`，否则下次打开会带上次数据。
- 提交中给按钮加 **loading**，防止重复点击：

```tsx
const [submitting, setSubmitting] = useState(false);
const onFinish = async (v) => {
  setSubmitting(true);
  try {
    await api.post('/merchants', v);
    message.success('添加成功');
    setModalOpen(false);
    form.resetFields();
    load();
  } finally {
    setSubmitting(false);
  }
};
<Button type="primary" htmlType="submit" loading={submitting}>确定</Button>
```

---

### 3.5 类型安全

- **api 泛型**：`api.get<{ list: Merchant[]; total: number }>(path)`，这样 `res.data` 有类型。
- **接口定义**：为列表行、表单值定义 interface，避免到处写 any。
- **可选链**：`user?.realName`、`location?.pathname ?? '/'`，避免未登录或未挂载时报错。

---

### 3.6 开发与生产环境

- **开发**：`npm run dev`，Vite 把 `/api` 代理到 `http://localhost:5000`，前端请求写 `/api/xxx` 即可。
- **生产**：打包后由 Nginx 等提供静态资源，需在 Nginx 里把 `/api` 反向代理到后端，否则生产会 404。

---

### 3.7 其他

- **StrictMode**：开发下可能让 effect 执行两次，用于发现副作用问题；若请求发两次，先看依赖是否写错。
- **key**：自己写 `list.map` 时用稳定 id 当 key，避免用 index（增删、排序会出问题）。
- **Button 的 type**：`type="primary"` 是样式；提交表单用 **htmlType="submit"**。

---

## 四、小结对照表

| 知识点 | 常用概念 | 在本项目中的体现 |
|--------|----------|------------------|
| 组件 | 函数组件、JSX、props | 各页面、Layout、PrivateRoute |
| 状态 | useState | 列表、分页、loading、弹窗、筛选条件 |
| 副作用 | useEffect | 进入页面或 page/pageSize 变化时请求列表 |
| 稳定引用 | useCallback | AuthContext 的 login、logout、setUser |
| 全局状态 | Context + useContext | AuthContext、useAuth、token/user |
| 路由 | Router、Route、Navigate、Outlet | App.tsx 路由表、Layout 的 Outlet、登录后跳转 |
| 请求 | fetch、封装 | api.get/post、401 跳登录、Bearer token |
| UI 库 | Ant Design | Form、Table、Modal、Button、message、Layout |
| 类型 | TypeScript、接口、泛型 | props 类型、api 泛型、Merchant/OrderRow 等接口 |

把上述示例和 `web/src` 里的实际代码对照着看，更容易形成肌肉记忆；遇到问题可先查“注意事项”和本节的错误/正确示例。
