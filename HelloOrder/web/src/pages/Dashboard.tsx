import { Card, Row, Col, Statistic } from 'antd';
import { ShopOutlined, FileTextOutlined, DollarOutlined } from '@ant-design/icons';

export default function Dashboard() {
  return (
    <div>
      <h2 style={{ marginBottom: 24 }}>工作台</h2>
      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={8}>
          <Card>
            <Statistic title="商家数" value={0} prefix={<ShopOutlined />} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Card>
            <Statistic title="待处理订单" value={0} prefix={<FileTextOutlined />} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Card>
            <Statistic title="本月销售额" value={0} prefix={<DollarOutlined />} suffix="CNY" />
          </Card>
        </Col>
      </Row>
      <Card title="快捷入口" style={{ marginTop: 24 }}>
        <p>从左侧菜单进入：商家管理、订单管理、报价单、采购单、仓储发货、报表与提成等。</p>
      </Card>
    </div>
  );
}
